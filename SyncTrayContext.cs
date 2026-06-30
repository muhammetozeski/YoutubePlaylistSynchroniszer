using System.Diagnostics;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The headless <c>--sync</c> run: no main window, just a tray icon so the user can double-click to open
/// the GUI while a background sync runs. Once the sync finishes the context exits the process. The actual
/// sync is wired in during the sync milestone; for now it shows the icon and exits cleanly.
/// </summary>
internal sealed class SyncTrayContext : ApplicationContext
{
    readonly NotifyIcon _tray;

    public SyncTrayContext()
    {
        _tray = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = Strings.TraySyncing,
            Visible = true,
        };
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.TrayOpen, null, (_, _) => OpenGui());
        menu.Items.Add(Strings.TrayExit, null, (_, _) => ExitThread());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenGui();
    }

    /// <summary>Opens the full GUI in a separate process (this one stays a background syncer).</summary>
    void OpenGui()
    {
        try { Process.Start(new ProcessStartInfo(AppConstants.ThisExePath, "--gui") { UseShellExecute = true }); }
        catch (Exception ex) { Log("Open GUI from tray failed: " + ex.Message, LogLevel.Warning); }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _tray.Visible = false; _tray.Dispose(); }
        base.Dispose(disposing);
    }
}
