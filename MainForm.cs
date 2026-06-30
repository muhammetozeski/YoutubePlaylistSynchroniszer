namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The application shell: a tabbed window where each feature lives on its own page (Account, Playlists,
/// Downloads, Settings, Logs). It also drives a sync run — feeding each playlist its own progress tab plus
/// the aggregate Downloads tab. Pages are docked UserControls, so the layout is resolution-independent.
/// </summary>
internal sealed class MainForm : Form
{
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    readonly AccountControl _account = new();
    readonly PlaylistsControl _playlists = new();
    readonly DownloadsControl _downloads = new();
    readonly SettingsControl _settings;
    readonly LogsControl _logs = new();

    TabPage _downloadsPage = null!;
    readonly List<TabPage> _syncTabs = [];
    bool _syncRunning;

    public MainForm()
    {
        Text = AppConstants.AppTitle;
        Icon = AppIcon.Shared;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 540);
        Size = new Size(Math.Max(MinimumSize.Width, Settings.WindowWidth), Math.Max(MinimumSize.Height, Settings.WindowHeight));
        Font = new Font("Segoe UI", 9f);

        _settings = new SettingsControl(ReapplyTheme);

        AddPage(Strings.TabAccount, _account);
        AddPage(Strings.TabPlaylists, _playlists);
        _downloadsPage = AddPage(Strings.TabDownloads, _downloads);
        AddPage(Strings.TabSettings, _settings);
        AddPage(Strings.TabLogs, _logs);

        Controls.Add(_tabs);

        _account.AuthChanged += _playlists.NotifyAuthChanged;
        _playlists.SyncRequested += StartSync;
        FormClosing += PersistWindowSize;
        Load += (_, _) => Theme.ApplyToWindow(this);
    }

    /// <summary>Re-themes the whole window (called by Settings when the dark-theme toggle changes).</summary>
    public void ReapplyTheme() => Theme.ApplyToWindow(this);

    TabPage AddPage(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(10) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        _tabs.TabPages.Add(page);
        return page;
    }

    async void StartSync(IReadOnlyList<SyncProfile> profiles)
    {
        if (_syncRunning) return;
        _syncRunning = true;

        // Drop the previous run's per-playlist tabs.
        foreach (var oldTab in _syncTabs) { _tabs.TabPages.Remove(oldTab); oldTab.Dispose(); }
        _syncTabs.Clear();

        var cancellation = new CancellationTokenSource();
        void Cancel() => cancellation.Cancel();

        _downloads.PrepareForRun();
        _downloads.CancelRequested += Cancel;

        var perPlaylist = new List<(SyncProfile profile, DownloadsControl view)>();
        foreach (var profile in profiles)
        {
            var view = new DownloadsControl { Dock = DockStyle.Fill };
            view.CancelRequested += Cancel;
            var page = new TabPage("⬇ " + profile.PlaylistTitle) { Padding = new Padding(10) };
            page.Controls.Add(view);
            _tabs.TabPages.Add(page);
            _syncTabs.Add(page);
            view.PrepareForRun();
            perPlaylist.Add((profile, view));
        }

        Theme.Apply(this); // theme the freshly added tabs
        _tabs.SelectedTab = _syncTabs.Count > 0 ? _syncTabs[0] : _downloadsPage;

        try
        {
            await Resilience.GuardAsync("GUI sync run", async () =>
            {
                if (Settings.AutoUpdateYtDlp.Value) await YtDlpManager.TryUpdateAsync(cancellation.Token);

                int downloaded = 0, skipped = 0, failed = 0, present = 0;
                foreach (var (profile, view) in perPlaylist)
                {
                    var observer = new CompositeSyncObserver(view, _downloads);
                    var summary = await SyncEngine.SyncPlaylistAsync(profile, observer, cancellation.Token);
                    view.Finish(string.Format(Strings.DownloadsSummaryFormat, summary.Downloaded, summary.SkippedLive, summary.Failed, summary.AlreadyPresent));
                    downloaded += summary.Downloaded; skipped += summary.SkippedLive; failed += summary.Failed; present += summary.AlreadyPresent;
                }

                if (Settings.CleanCacheAfterSync.Value) SyncEngine.CleanCache();
                _downloads.Finish(string.Format(Strings.DownloadsSummaryFormat, downloaded, skipped, failed, present));
            });
        }
        finally
        {
            _downloads.CancelRequested -= Cancel;
            cancellation.Dispose();
            _syncRunning = false;
        }
    }

    void PersistWindowSize(object? sender, FormClosingEventArgs e)
    {
        if (WindowState != FormWindowState.Normal) return;
        Settings.WindowWidth.Value = Width;
        Settings.WindowHeight.Value = Height;
        SettingsManager.SaveSettings();
    }
}
