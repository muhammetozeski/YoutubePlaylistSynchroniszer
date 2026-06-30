namespace YoutubePlaylistSynchroniszer;

/// <summary>Fans every sync callback out to several observers — used to feed a playlist's own page and the
/// aggregate Downloads page at once.</summary>
internal sealed class CompositeSyncObserver(params ISyncObserver[] observers) : ISyncObserver
{
    public void OnPhase(string message)
    {
        foreach (var observer in observers) observer.OnPhase(message);
    }

    public void OnPlaylistScanned(int playlistTotal, int alreadyPresent, int toDownload)
    {
        foreach (var observer in observers) observer.OnPlaylistScanned(playlistTotal, alreadyPresent, toDownload);
    }

    public void OnQueued(IReadOnlyList<PlaylistVideo> queued, string targetFolder)
    {
        foreach (var observer in observers) observer.OnQueued(queued, targetFolder);
    }

    public void OnItemStarted(string videoId, string title, int index, int total)
    {
        foreach (var observer in observers) observer.OnItemStarted(videoId, title, index, total);
    }

    public void OnItemProgress(string videoId, double percent)
    {
        foreach (var observer in observers) observer.OnItemProgress(videoId, percent);
    }

    public void OnItemFinished(string videoId, DownloadOutcome outcome, string? message, string? filePath)
    {
        foreach (var observer in observers) observer.OnItemFinished(videoId, outcome, message, filePath);
    }
}
