namespace YoutubePlaylistSynchroniszer;

/// <summary>What happened to one video during a sync.</summary>
internal enum DownloadOutcome { Downloaded, SkippedLive, Failed }

/// <summary>Result of downloading a single video.</summary>
internal sealed record VideoDownloadResult(DownloadOutcome Outcome, string? FilePath, string? Message);

/// <summary>Tallies for a finished sync run.</summary>
internal sealed record SyncSummary(int Total, int AlreadyPresent, int Downloaded, int SkippedLive, int Failed);

/// <summary>
/// Receives progress from a sync run. The engine calls these from worker threads, so a UI implementation
/// must marshal to the UI thread itself.
/// </summary>
internal interface ISyncObserver
{
    void OnPhase(string message);
    void OnPlaylistScanned(int playlistTotal, int alreadyPresent, int toDownload);
    /// <summary>The full set of videos that will be downloaded, reported up front (before any download
    /// starts) so the UI can list them all at once rather than one-by-one. <paramref name="targetFolder"/>
    /// is where they sync to.</summary>
    void OnQueued(IReadOnlyList<PlaylistVideo> queued, string targetFolder);
    void OnItemStarted(string videoId, string title, int index, int total);
    void OnItemProgress(string videoId, double percent);
    void OnItemFinished(string videoId, DownloadOutcome outcome, string? message, string? filePath);
}
