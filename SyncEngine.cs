namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The core sync: for a playlist, enumerate its videos, scan the target folder for ones already present
/// (by the "(videoId)" in each filename, excluding image files), and download only the missing ones —
/// straight into the target folder. Reports progress through an optional <see cref="ISyncObserver"/>.
/// </summary>
internal static class SyncEngine
{
    public static Task<SyncSummary> SyncPlaylistAsync(SyncProfile profile, ISyncObserver? observer = null, CancellationToken cancellationToken = default) =>
        Resilience.RunAsync($"Sync playlist '{profile.PlaylistTitle}'", async token =>
        {
            observer?.OnPhase(Strings.DownloadsStatusEnumerating);
            // A playlist can list the same video more than once; de-duplicate by id so two tasks never
            // share a per-video work folder (which would race on the file move) and the tallies stay correct.
            var videos = (await YouTubeApiClient.ListPlaylistVideosAsync(profile.PlaylistId, token))
                .DistinctBy(v => v.Id).ToList();

            observer?.OnPhase(Strings.DownloadsStatusScanning);
            string targetFolder = profile.EffectiveTargetFolder; // explicit folder, else the kind's default
            var existing = ScanExistingVideoIds(targetFolder);
            var missing = videos.Where(v => !existing.Contains(v.Id)).ToList();
            int alreadyPresent = videos.Count - missing.Count;
            observer?.OnPlaylistScanned(videos.Count, alreadyPresent, missing.Count);
            observer?.OnQueued(missing); // list everything up front, then download

            int downloaded = 0, skipped = 0, failed = 0, index = 0;
            using var gate = new SemaphoreSlim(Math.Max(1, Settings.MaxConcurrentDownloads.Value));

            var tasks = missing.Select(async video =>
            {
                await gate.WaitAsync(token);
                try
                {
                    int itemIndex = Interlocked.Increment(ref index);
                    observer?.OnItemStarted(video.Id, video.Title, itemIndex, missing.Count);

                    var progress = new Progress<double>(percent => observer?.OnItemProgress(video.Id, percent));
                    var result = await YtDlpManager.DownloadVideoAsync(video, targetFolder, profile.Options, progress, token);

                    switch (result.Outcome)
                    {
                        case DownloadOutcome.Downloaded: Interlocked.Increment(ref downloaded); break;
                        case DownloadOutcome.SkippedLive: Interlocked.Increment(ref skipped); break;
                        default: Interlocked.Increment(ref failed); break;
                    }
                    observer?.OnItemFinished(video.Id, result.Outcome, result.Message);
                }
                finally { gate.Release(); }
            });
            await Task.WhenAll(tasks);

            var summary = new SyncSummary(videos.Count, alreadyPresent, downloaded, skipped, failed);
            observer?.OnPhase(string.Format(Strings.DownloadsSummaryFormat, downloaded, skipped, failed, alreadyPresent));
            return summary;
        }, input: profile.PlaylistId, cancellationToken: cancellationToken);

    /// <summary>Runs every supplied profile in turn, then (if enabled) clears the download cache.</summary>
    public static async Task<SyncSummary> SyncAllAsync(IEnumerable<SyncProfile> profiles, ISyncObserver? observer = null, CancellationToken cancellationToken = default)
    {
        int total = 0, present = 0, downloaded = 0, skipped = 0, failed = 0;
        foreach (var profile in profiles)
        {
            if (!profile.IsReadyToSync) { Log($"Skipping incomplete profile '{profile.PlaylistTitle}'.", LogLevel.Warning); continue; }
            var summary = await SyncPlaylistAsync(profile, observer, cancellationToken);
            total += summary.Total; present += summary.AlreadyPresent;
            downloaded += summary.Downloaded; skipped += summary.SkippedLive; failed += summary.Failed;
        }

        if (Settings.CleanCacheAfterSync.Value) CleanCache();
        return new SyncSummary(total, present, downloaded, skipped, failed);
    }

    /// <summary>Collects the YouTube ids already present in the target folder. Image files are excluded so a
    /// thumbnail never masks a missing video (the spec's exclude-only-images rule).</summary>
    public static HashSet<string> ScanExistingVideoIds(string targetFolder)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (!Directory.Exists(targetFolder)) return ids;

        foreach (var file in Directory.EnumerateFiles(targetFolder, "*", SearchOption.AllDirectories))
        {
            if (MediaScan.IsImageExtension(Path.GetExtension(file))) continue;
            string? id = ExtractVideoId(Path.GetFileNameWithoutExtension(file));
            if (id is not null) ids.Add(id);
        }
        return ids;
    }

    /// <summary>Removes the per-video download work folders, keeping the bootstrapped tools.</summary>
    public static void CleanCache()
    {
        try
        {
            if (!Directory.Exists(ConfigPathResolver.CacheFolder)) return;
            foreach (var directory in Directory.EnumerateDirectories(ConfigPathResolver.CacheFolder))
            {
                if (Path.GetFileName(directory).Equals("tools", StringComparison.OrdinalIgnoreCase)) continue;
                try { Directory.Delete(directory, recursive: true); }
                catch (Exception ex) { Log("Cache cleanup (" + directory + "): " + ex.Message, LogLevel.Warning); }
            }
            Log("Download cache cleaned.", LogLevel.Info);
        }
        catch (Exception ex) { Log("Cache cleanup failed: " + ex.Message, LogLevel.Warning); }
    }
}
