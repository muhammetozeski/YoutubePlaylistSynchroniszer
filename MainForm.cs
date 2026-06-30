namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The application shell: a tabbed window where each feature lives on its own page (Account, Playlists,
/// Downloads, Settings, Logs). Pages are <see cref="UserControl"/>s docked to fill, so the whole layout
/// is resolution-independent (no absolute coordinates).
/// </summary>
internal sealed class MainForm : Form
{
    readonly TabControl _tabs = new() { Dock = DockStyle.Fill };

    public MainForm()
    {
        Text = AppConstants.AppTitle;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 540);
        Size = new Size(Math.Max(MinimumSize.Width, Settings.WindowWidth), Math.Max(MinimumSize.Height, Settings.WindowHeight));
        Font = new Font("Segoe UI", 9f);

        // Real feature pages are added as their UserControls are built; the shell only owns the tabs.
        AddPage(Strings.TabAccount);
        AddPage(Strings.TabPlaylists);
        AddPage(Strings.TabDownloads);
        AddPage(Strings.TabSettings);
        AddPage(Strings.TabLogs);

        Controls.Add(_tabs);
        FormClosing += PersistWindowSize;
    }

    void AddPage(string title, Control? content = null)
    {
        var page = new TabPage(title) { Padding = new Padding(10) };
        if (content is not null) { content.Dock = DockStyle.Fill; page.Controls.Add(content); }
        _tabs.TabPages.Add(page);
    }

    void PersistWindowSize(object? sender, FormClosingEventArgs e)
    {
        if (WindowState != FormWindowState.Normal) return;
        Settings.WindowWidth.Value = Width;
        Settings.WindowHeight.Value = Height;
        SettingsManager.SaveSettings();
    }
}
