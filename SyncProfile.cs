namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// A saved sync setup for one playlist: which playlist, where it syncs to, how it downloads, and whether
/// the headless background run should include it. These are what the <c>--sync</c> mode replays.
/// </summary>
internal sealed class SyncProfile
{
    public string PlaylistId { get; set; } = "";
    public string PlaylistTitle { get; set; } = "";
    public string TargetFolder { get; set; } = "";
    public bool EnabledForBackgroundSync { get; set; } = true;
    public DownloadOptions Options { get; set; } = new();

    public bool IsReadyToSync =>
        !string.IsNullOrWhiteSpace(PlaylistId) && !string.IsNullOrWhiteSpace(TargetFolder);
}
