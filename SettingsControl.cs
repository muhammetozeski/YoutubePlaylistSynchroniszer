namespace YoutubePlaylistSynchroniszer;

/// <summary>The Settings tab: app-wide preferences, each persisted immediately on change.</summary>
internal sealed class SettingsControl : UserControl
{
    readonly Action _onThemeChanged;

    public SettingsControl(Action onThemeChanged)
    {
        _onThemeChanged = onThemeChanged;
        Dock = DockStyle.Fill;
        AutoScroll = true;

        var layout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4),
        };

        layout.Controls.Add(Ui.Header(Strings.SettingsHeader));
        layout.Controls.Add(BuildLanguageRow());
        layout.Controls.Add(Ui.Check(Strings.SettingsDarkThemeLabel, Settings.DarkTheme, (s, _) =>
        {
            Persist(() => Settings.DarkTheme.Value = ((CheckBox)s!).Checked);
            _onThemeChanged();
        }));
        layout.Controls.Add(Ui.Check(Strings.SettingsBulkConfirmLabel, Settings.ConfirmBulkSelect, (s, _) =>
            Persist(() => Settings.ConfirmBulkSelect.Value = ((CheckBox)s!).Checked)));
        layout.Controls.Add(Ui.Check(Strings.SettingsLoggingLabel, Settings.EnableLogging, (s, _) =>
            LoggerHost.SetEnabled(((CheckBox)s!).Checked)));
        layout.Controls.Add(Ui.Check(Strings.SettingsCleanCacheLabel, Settings.CleanCacheAfterSync, (s, _) =>
            Persist(() => Settings.CleanCacheAfterSync.Value = ((CheckBox)s!).Checked)));
        layout.Controls.Add(Ui.Check(Strings.SettingsSkipLiveLabel, Settings.SkipLiveStreams, (s, _) =>
            Persist(() => Settings.SkipLiveStreams.Value = ((CheckBox)s!).Checked)));
        layout.Controls.Add(Ui.Check(Strings.SettingsAutoUpdateYtDlpLabel, Settings.AutoUpdateYtDlp, (s, _) =>
            Persist(() => Settings.AutoUpdateYtDlp.Value = ((CheckBox)s!).Checked)));
        layout.Controls.Add(BuildConcurrencyRow());
        layout.Controls.Add(BuildMaxDurationRow());

        Controls.Add(layout);
    }

    Control BuildLanguageRow()
    {
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        row.Controls.Add(Ui.Label(Strings.SettingsLanguageLabel));

        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Margin = new Padding(4, 4, 4, 4) };
        var languages = LocManager.Available;
        combo.Items.AddRange([.. languages.Select(l => (object)l.Name)]);
        int current = Array.FindIndex(languages, l => l.Code.Equals(Settings.Language.Value, StringComparison.OrdinalIgnoreCase));
        combo.SelectedIndex = current >= 0 ? current : 0;
        combo.SelectedIndexChanged += (_, _) =>
        {
            if (combo.SelectedIndex < 0 || combo.SelectedIndex >= languages.Length) return;
            Persist(() => Settings.Language.Value = languages[combo.SelectedIndex].Code);
            MessageBox.Show(Strings.SettingsLanguageRestartNote, AppConstants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        row.Controls.Add(combo);
        return row;
    }

    Control BuildConcurrencyRow()
    {
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        row.Controls.Add(Ui.Label(Strings.SettingsConcurrencyLabel));

        var spinner = new NumericUpDown { Minimum = 1, Maximum = 8, Value = Math.Clamp(Settings.MaxConcurrentDownloads.Value, 1, 8), Width = 60, Margin = new Padding(4) };
        spinner.ValueChanged += (_, _) => Persist(() => Settings.MaxConcurrentDownloads.Value = (int)spinner.Value);
        row.Controls.Add(spinner);
        return row;
    }

    Control BuildMaxDurationRow()
    {
        var row = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        row.Controls.Add(Ui.Label(Strings.SettingsMaxDurationLabel));

        var spinner = new NumericUpDown { Minimum = 0, Maximum = 100000, Value = Math.Clamp(Settings.MaxVideoDurationMinutes.Value, 0, 100000), Width = 90, Margin = new Padding(4) };
        spinner.ValueChanged += (_, _) => Persist(() => Settings.MaxVideoDurationMinutes.Value = (int)spinner.Value);
        row.Controls.Add(spinner);
        return row;
    }

    static void Persist(Action change)
    {
        change();
        SettingsManager.SaveSettings();
    }
}
