namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Global constants and configuration defaults. Endpoints, folder names, file-name conventions and
/// CLI metadata are centralized here so a single edit changes them everywhere (per the project's
/// "central management" principle).
/// </summary>
internal static class AppConstants
{
    public const string AppTitle = "YouTube Playlist Synchronizer";
    public const string AppFolderName = "YoutubePlaylistSynchroniszer";
    public const string Version = "0.2.3";

    /// <summary>Full path to the running executable. Not null for file-based exes.</summary>
    public static readonly string ThisExePath = Environment.ProcessPath ?? Application.ExecutablePath;

    /// <summary>Folder the executable lives in.</summary>
    public static readonly string ThisExeFolder = Path.GetDirectoryName(ThisExePath) ?? AppContext.BaseDirectory;

    // ---- Data folders (next to the exe) ----
    public const string UserDataFolderName = "UserData";
    public const string CacheFolderName = "Cache";

    // ---- Config file (a single key=value text file under UserData) ----
    public const string ConfigFileName = "settings.config";
    public const string CommentPrefix = "#";
    public const string KeyValueSeparator = "=";

    // ---- Google OAuth 2.0 (installed-app / loopback flow) ----
    public const string GoogleAuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string GoogleTokenEndpoint = "https://oauth2.googleapis.com/token";
    /// <summary>Read-only access is enough to enumerate the user's playlists and their items.</summary>
    public const string YouTubeReadOnlyScope = "https://www.googleapis.com/auth/youtube.readonly";

    // ---- YouTube Data API v3 ----
    public const string YouTubeApiBase = "https://www.googleapis.com/youtube/v3";

    // ---- External tools (resolved on PATH, else bootstrapped into the cache) ----
    public const string YtDlpExeName = "yt-dlp.exe";
    public const string FfmpegExeName = "ffmpeg.exe";
    public const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    // ---- File-name convention shared with the reference ps1: "Title (VIDEOID).ext" ----
    public const string DownloadOutputTemplate = "%(title)s (%(id)s).%(ext)s";
}
