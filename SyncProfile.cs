using System.Text.Json.Serialization;

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

    /// <summary>The folder this playlist actually syncs to: its explicit <see cref="TargetFolder"/>, else
    /// the default folder for its media kind (audio/video) from Settings. Computed, never stored — so
    /// setting a default folder makes playlists ready without writing per-playlist JSON.</summary>
    [JsonIgnore]
    public string EffectiveTargetFolder =>
        !string.IsNullOrWhiteSpace(TargetFolder) ? TargetFolder
        : Options.Kind == MediaKind.Video ? Settings.DefaultVideoFolder.Value : Settings.DefaultAudioFolder.Value;

    [JsonIgnore]
    public bool IsReadyToSync =>
        !string.IsNullOrWhiteSpace(PlaylistId) && !string.IsNullOrWhiteSpace(EffectiveTargetFolder);
}
