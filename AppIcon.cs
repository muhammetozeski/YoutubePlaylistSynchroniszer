namespace YoutubePlaylistSynchroniszer;

/// <summary>The app icon, read once from the running exe (the embedded ApplicationIcon). Works under
/// single-file publish, where no loose <c>app.ico</c> sits next to the exe. Falls back to a system icon.</summary>
internal static class AppIcon
{
    static Icon? _shared;

    public static Icon Shared => _shared ??= Load();

    static Icon Load()
    {
        try { return Icon.ExtractAssociatedIcon(AppConstants.ThisExePath) ?? SystemIcons.Application; }
        catch (Exception ex) { Log("App icon load failed: " + ex.Message, LogLevel.Warning); return SystemIcons.Application; }
    }
}
