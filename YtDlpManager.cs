using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Locates (or bootstraps) yt-dlp and ffmpeg and drives a single-video download. Each video is downloaded
/// into a per-video work folder under <c>Cache</c>; only the finished media file (never a stray thumbnail)
/// is moved into the user's target folder, and the work folder is removed afterwards.
/// </summary>
internal static partial class YtDlpManager
{
    static string? _ytDlpPath;
    static string? _ffmpegPath;

    /// <summary>Resolves the external tools, downloading yt-dlp into the cache if it is not already on PATH.</summary>
    public static async Task EnsureToolsAsync(CancellationToken cancellationToken = default)
    {
        _ffmpegPath ??= ResolveOnPath(AppConstants.FfmpegExeName);
        if (_ffmpegPath is null) Log("ffmpeg not found on PATH; thumbnail embedding / format conversion may be limited.", LogLevel.Warning);

        _ytDlpPath ??= ResolveOnPath(AppConstants.YtDlpExeName) ?? await BootstrapYtDlpAsync(cancellationToken);
        if (_ytDlpPath is null) throw new FileNotFoundException("yt-dlp could not be located or downloaded.");
    }

    /// <summary>Updates yt-dlp only when we own the binary (a cache copy); a PATH/scoop copy is left to its
    /// own package manager.</summary>
    public static async Task TryUpdateAsync(CancellationToken cancellationToken = default)
    {
        if (_ytDlpPath is null || !_ytDlpPath.StartsWith(ConfigPathResolver.ToolsFolder, StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            await Resilience.RunAsync("yt-dlp self-update",
                ct => ProcessRunner.RunAsync(_ytDlpPath!, ["-U"], onOutputLine: line => Log("[yt-dlp -U] " + line), cancellationToken: ct),
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) { Log("yt-dlp self-update skipped: " + ex.Message, LogLevel.Warning); }
    }

    public static Task<VideoDownloadResult> DownloadVideoAsync(PlaylistVideo video, string targetFolder, DownloadOptions options,
        IProgress<double>? progress = null, CancellationToken cancellationToken = default) =>
        Resilience.RunAsync($"Download video {video.Id}", async token =>
        {
            await EnsureToolsAsync(token);
            Directory.CreateDirectory(targetFolder);

            string workFolder = Path.Combine(ConfigPathResolver.CacheFolder, "dl_" + video.Id);
            TryDeleteFolder(workFolder);
            Directory.CreateDirectory(workFolder);
            try
            {
                bool filteredOut = false;
                var run = await ProcessRunner.RunAsync(_ytDlpPath!, BuildDownloadArguments(video.Id, workFolder, options), workFolder, line =>
                {
                    var match = ProgressRegex().Match(line);
                    if (match.Success && double.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out double percent))
                        progress?.Report(percent);
                    else if (line.Contains("does not pass filter", StringComparison.OrdinalIgnoreCase))
                        filteredOut = true;
                }, token);

                string? media = FindMediaFile(workFolder, video.Id);
                if (media is null)
                    return filteredOut
                        ? new VideoDownloadResult(DownloadOutcome.SkippedLive, null, null)
                        : new VideoDownloadResult(DownloadOutcome.Failed, null, ShortError(run.StandardError));

                if (options is { Kind: MediaKind.Music, MusicTier: MusicQualityTier.Worst, ConvertWorstToCodec2: true })
                    media = await ConvertToCodec2Async(media, token) ?? media;

                string finalPath = MoveToTarget(media, targetFolder);
                progress?.Report(100);
                return new VideoDownloadResult(DownloadOutcome.Downloaded, finalPath, null);
            }
            finally { TryDeleteFolder(workFolder); }
        }, input: video.Id, cancellationToken: cancellationToken);

    static IReadOnlyList<string> BuildDownloadArguments(string videoId, string workFolder, DownloadOptions options)
    {
        var arguments = new List<string>
        {
            "--no-playlist", "--ignore-errors", "--no-overwrites", "--continue", "--newline", "--no-warnings",
            "--retries", Settings.YtDlpRetries.Value.ToString(),
            "--fragment-retries", Settings.YtDlpRetries.Value.ToString(),
        };
        if (_ffmpegPath is not null) arguments.AddRange(["--ffmpeg-location", _ffmpegPath]);

        // Combine all skip conditions into ONE match-filter (yt-dlp ANDs within a filter, ORs across them).
        var filters = new List<string>();
        if (Settings.SkipLiveStreams.Value)
            filters.Add("live_status != is_live & live_status != is_upcoming & live_status != post_live");
        int maxMinutes = Settings.MaxVideoDurationMinutes.Value;
        if (maxMinutes > 0)
            filters.Add($"duration <=? {maxMinutes * 60}"); // <=? lets unknown-duration videos still download
        if (filters.Count > 0)
            arguments.AddRange(["--match-filter", string.Join(" & ", filters)]);
        arguments.AddRange(options.BuildFormatArguments());
        arguments.AddRange(["-o", Path.Combine(workFolder, AppConstants.DownloadOutputTemplate)]);
        arguments.Add(AppConstants.ShortVideoUrlBase + videoId);
        return arguments;
    }

    /// <summary>The downloaded media file in the work folder: matches the id, is not an image, and is not a
    /// partial/temp artifact. Picks the largest match when more than one survives.</summary>
    static string? FindMediaFile(string folder, string videoId) =>
        Directory.EnumerateFiles(folder)
            .Where(f => f.Contains("(" + videoId + ")", StringComparison.OrdinalIgnoreCase))
            .Where(f => !MediaScan.IsImageExtension(Path.GetExtension(f)))
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is not (".part" or ".ytdl" or ".temp"))
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();

    static string MoveToTarget(string mediaFile, string targetFolder)
    {
        string destination = Path.Combine(targetFolder, Path.GetFileName(mediaFile));
        File.Move(mediaFile, destination, overwrite: true);
        return destination;
    }

    static async Task<string?> ConvertToCodec2Async(string input, CancellationToken cancellationToken)
    {
        if (_ffmpegPath is null) { Log("Codec2 conversion skipped: ffmpeg unavailable.", LogLevel.Warning); return null; }
        string output = Path.ChangeExtension(input, ".c2");
        var run = await ProcessRunner.RunAsync(_ffmpegPath,
            ["-y", "-i", input, "-c:a", "libcodec2", "-mode", "700C", output],
            Path.GetDirectoryName(input), line => Log("[ffmpeg] " + line), cancellationToken);

        if (run.ExitCode == 0 && File.Exists(output))
        {
            try { File.Delete(input); } catch { }
            return output;
        }
        Log("Codec2 conversion failed (keeping original audio): " + ShortError(run.StandardError), LogLevel.Warning);
        return null;
    }

    static async Task<string?> BootstrapYtDlpAsync(CancellationToken cancellationToken) =>
        await Resilience.RunAsync("Bootstrap yt-dlp into cache", async token =>
        {
            Directory.CreateDirectory(ConfigPathResolver.ToolsFolder);
            string destination = Path.Combine(ConfigPathResolver.ToolsFolder, AppConstants.YtDlpExeName);
            if (File.Exists(destination)) return destination;

            using var response = await HttpClientProvider.Shared.GetAsync(
                AppConstants.YtDlpDownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using (var file = File.Create(destination))
                await response.Content.CopyToAsync(file, token);
            Log("yt-dlp bootstrapped to " + destination, LogLevel.Info);
            return destination;
        }, input: AppConstants.YtDlpDownloadUrl, pipeline: Resilience.NetworkPipeline, cancellationToken: cancellationToken);

    /// <summary>First match for the exe across PATH directories plus our own tools folder.</summary>
    static string? ResolveOnPath(string exeName)
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (var directory in directories.Append(ConfigPathResolver.ToolsFolder))
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory)) continue;
                string candidate = Path.Combine(directory.Trim(), exeName);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }

    static void TryDeleteFolder(string folder)
    {
        try { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
        catch (Exception ex) { Log("Could not remove work folder " + folder + ": " + ex.Message, LogLevel.Warning); }
    }

    static string ShortError(string standardError)
    {
        var errorLine = standardError.Split('\n')
            .Select(l => l.Trim())
            .LastOrDefault(l => l.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase));
        string text = string.IsNullOrEmpty(errorLine) ? standardError.Trim() : errorLine;
        return text.Length <= 300 ? text : text[..300] + "…";
    }

    [GeneratedRegex(@"\[download\]\s+([\d.]+)%")]
    private static partial Regex ProgressRegex();
}
