namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Resolves where the app keeps its data. Per the spec, everything durable lives under a
/// <c>UserData</c> folder next to the exe and transient downloads go to a <c>Cache</c> folder next to
/// the exe. If the exe folder is not writable (e.g. installed under Program Files), both fall back to
/// <c>%AppData%\YoutubePlaylistSynchroniszer\…</c> so the app still runs.
/// </summary>
internal static class ConfigPathResolver
{
    static string? _dataFolder;
    static string? _cacheFolder;

    /// <summary>Durable data root: <c>&lt;exe&gt;\UserData</c> when writable, else under %AppData%.</summary>
    public static string DataFolder => _dataFolder ??= ResolveFolder(AppConstants.UserDataFolderName);

    /// <summary>Transient download scratch: <c>&lt;exe&gt;\Cache</c> when writable, else under %AppData%.</summary>
    public static string CacheFolder => _cacheFolder ??= ResolveFolder(AppConstants.CacheFolderName);

    public static string ConfigFolder => DataFolder;
    public static string ConfigPath => Path.Combine(DataFolder, AppConstants.ConfigFileName);
    public static string LogsFolder => Path.Combine(DataFolder, "Logs");

    /// <summary>DPAPI-encrypted Google client secret JSON.</summary>
    public static string CredentialsPath => Path.Combine(DataFolder, "credentials.enc");
    /// <summary>DPAPI-encrypted OAuth refresh token.</summary>
    public static string RefreshTokenPath => Path.Combine(DataFolder, "refresh.enc");
    /// <summary>Saved per-playlist sync profiles (JSON).</summary>
    public static string ProfilesPath => Path.Combine(DataFolder, "profiles.json");
    /// <summary>Folder for bootstrapped external tools (yt-dlp, ffmpeg) under the cache.</summary>
    public static string ToolsFolder => Path.Combine(CacheFolder, "tools");

    static string ResolveFolder(string folderName)
    {
        try
        {
            string exeFolder = AppConstants.ThisExeFolder;
            if (!string.IsNullOrEmpty(exeFolder) && IsWritable(exeFolder))
            {
                string target = Path.Combine(exeFolder, folderName);
                Directory.CreateDirectory(target);
                return target;
            }
        }
        catch (Exception ex) { Log("Data folder probe failed: " + ex.Message, LogLevel.Warning); }

        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            AppConstants.AppFolderName, folderName);
        try { Directory.CreateDirectory(appData); } catch (Exception ex) { Log("AppData dir create failed: " + ex.Message, LogLevel.Warning); }
        return appData;
    }

    static bool IsWritable(string folder)
    {
        try
        {
            string probe = Path.Combine(folder, ".write_" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(probe, "x");
            File.Delete(probe);
            return true;
        }
        catch { return false; }
    }
}
