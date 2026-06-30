namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The application shell: a tabbed window where each feature lives on its own page (Account, Playlists,
/// Downloads, Settings, Logs). Pages are <see cref="UserControl"/>s docked to fill, so the whole layout
/// is resolution-independent (no absolute coordinates). The shell only wires the pages together.
/// </summary>
internal sealed class MainForm : Form
{
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    readonly AccountControl _account = new();
    readonly PlaylistsControl _playlists = new();
    readonly DownloadsControl _downloads = new();
    readonly SettingsControl _settings = new();
    readonly LogsControl _logs = new();

    TabPage _downloadsPage = null!;

    public MainForm()
    {
        Text = AppConstants.AppTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 540);
        Size = new Size(Math.Max(MinimumSize.Width, Settings.WindowWidth), Math.Max(MinimumSize.Height, Settings.WindowHeight));
        Font = new Font("Segoe UI", 9f);

        AddPage(Strings.TabAccount, _account);
        AddPage(Strings.TabPlaylists, _playlists);
        _downloadsPage = AddPage(Strings.TabDownloads, _downloads);
        AddPage(Strings.TabSettings, _settings);
        AddPage(Strings.TabLogs, _logs);

        Controls.Add(_tabs);

        _account.AuthChanged += _playlists.NotifyAuthChanged;
        _playlists.SyncRequested += StartSync;
        FormClosing += PersistWindowSize;
    }

    TabPage AddPage(string title, Control content)
    {
        var page = new TabPage(title) { Padding = new Padding(10) };
        content.Dock = DockStyle.Fill;
        page.Controls.Add(content);
        _tabs.TabPages.Add(page);
        return page;
    }

    void StartSync(IReadOnlyList<SyncProfile> profiles)
    {
        _tabs.SelectedTab = _downloadsPage;
        _downloads.Run(profiles);
    }

    void PersistWindowSize(object? sender, FormClosingEventArgs e)
    {
        if (WindowState != FormWindowState.Normal) return;
        Settings.WindowWidth.Value = Width;
        Settings.WindowHeight.Value = Height;
        SettingsManager.SaveSettings();
    }
}
