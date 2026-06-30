using System.Diagnostics;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The headless <c>--sync</c> run: no main window, just a tray icon so the user can double-click to open
/// the GUI while a background sync runs. It replays the profiles marked for background sync, logs
/// everything, shows a finishing balloon, then exits the process. The whole run is guarded by
/// <see cref="Resilience.GuardAsync"/>.
/// </summary>
internal sealed class SyncTrayContext : ApplicationContext
{
    readonly NotifyIcon _tray;
    readonly CancellationTokenSource _cancellation = new();

    public SyncTrayContext()
    {
        _tray = new NotifyIcon { Icon = AppIcon.Shared, Text = Strings.TraySyncing, Visible = true };
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.TrayOpen, null, (_, _) => OpenGui());
        menu.Items.Add(Strings.TrayExit, null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenGui();

        _ = RunSyncThenExitAsync();
    }

    async Task RunSyncThenExitAsync()
    {
        await Resilience.GuardAsync("Headless background sync", async () =>
        {
            var profiles = SyncProfileStore.All.Where(p => p.EnabledForBackgroundSync && p.IsReadyToSync).ToList();
            if (profiles.Count == 0) { Log(Strings.CliNoProfiles, LogLevel.Warning); return; }
            if (!CredentialStore.IsAuthorized) { Log("Background sync: not authorized; nothing to do.", LogLevel.Warning); return; }

            if (Settings.AutoUpdateYtDlp.Value) await YtDlpManager.TryUpdateAsync(_cancellation.Token);
            var summary = await SyncEngine.SyncAllAsync(profiles, null, _cancellation.Token);
            ShowBalloon(string.Format(Strings.CliSyncDoneFormat, summary.Downloaded, summary.Failed));
        });

        // Let a finishing balloon show briefly, then close (the run is "sync and exit").
        try { await Task.Delay(2500, _cancellation.Token); } catch { }
        Shutdown();
    }

    void ShowBalloon(string text)
    {
        try
        {
            _tray.BalloonTipTitle = AppConstants.AppTitle;
            _tray.BalloonTipText = text;
            _tray.ShowBalloonTip(4000);
        }
        catch (Exception ex) { Log("Balloon tip failed: " + ex.Message, LogLevel.Warning); }
    }

    /// <summary>Opens the full GUI in a separate process (this one stays a background syncer).</summary>
    void OpenGui()
    {
        try { Process.Start(new ProcessStartInfo(AppConstants.ThisExePath, "--gui") { UseShellExecute = true }); }
        catch (Exception ex) { Log("Open GUI from tray failed: " + ex.Message, LogLevel.Warning); }
    }

    void Shutdown()
    {
        try { _cancellation.Cancel(); } catch { }
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Visible = false;
            _tray.Dispose();
            _cancellation.Dispose();
        }
        base.Dispose(disposing);
    }
}
