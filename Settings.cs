namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// All globally accessible settings. Each public static readonly Setting&lt;T&gt; field is
/// auto-registered by <see cref="SettingsManager"/> (its key is the field name). Per-playlist sync
/// options live in <see cref="SyncProfile"/> instead; this holds only app-wide preferences.
/// </summary>
internal static class Settings
{
    /// <summary>Master logging on/off. On by default — the spec requires every operation to be logged.</summary>
    public static readonly Setting<bool> EnableLogging = new(true);

    /// <summary>UI language code ("tr" or "en"). Loaded into <see cref="Strings"/> at startup. Turkish default.</summary>
    public static readonly Setting<string> Language = new("tr");

    /// <summary>Cached channel title of the signed-in account, shown as the "connected as …" label.</summary>
    public static readonly Setting<string> AccountLabel = new("");

    /// <summary>Dark UI theme on/off.</summary>
    public static readonly Setting<bool> DarkTheme = new(false);

    /// <summary>Ask for confirmation on bulk select-all / clear-all. Turned off by the "don't ask again"
    /// option and re-enabled from Settings.</summary>
    public static readonly Setting<bool> ConfirmBulkSelect = new(true);

    /// <summary>Delete the Cache folder contents after a sync run finishes.</summary>
    public static readonly Setting<bool> CleanCacheAfterSync = new(true);

    /// <summary>Skip live / upcoming / just-ended streams during downloads (mirrors the reference ps1).</summary>
    public static readonly Setting<bool> SkipLiveStreams = new(true);

    /// <summary>How many videos download in parallel within one playlist sync.</summary>
    public static readonly Setting<int> MaxConcurrentDownloads = new(1);

    /// <summary>Try to update yt-dlp (when bootstrapped into the cache) before a sync run.</summary>
    public static readonly Setting<bool> AutoUpdateYtDlp = new(true);

    /// <summary>Retry count handed to yt-dlp for transient network failures per video.</summary>
    public static readonly Setting<int> YtDlpRetries = new(10);

    /// <summary>Skip videos longer than this many minutes (0 = no limit). Default 3 hours. A playlist can
    /// override this via <see cref="DownloadOptions.MaxDurationMinutesOverride"/>.</summary>
    public static readonly Setting<int> MaxVideoDurationMinutes = new(180);

    /// <summary>Default target folder applied by the "audio preset" bulk action.</summary>
    public static readonly Setting<string> DefaultAudioFolder = new("");

    /// <summary>Default target folder applied by the "video preset" bulk action.</summary>
    public static readonly Setting<string> DefaultVideoFolder = new("");

    /// <summary>Persisted main-window size so the responsive layout reopens where the user left it.</summary>
    public static readonly Setting<int> WindowWidth = new(1000);
    public static readonly Setting<int> WindowHeight = new(680);
}
