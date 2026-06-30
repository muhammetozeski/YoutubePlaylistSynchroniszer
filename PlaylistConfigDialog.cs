namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Edits one playlist's sync setup: the target folder and the download options (music tier / custom
/// format+bitrate / worst+codec2, or video height). Music and video option groups show only when their
/// media kind is selected; the custom/worst sub-options enable only for their tier.
/// </summary>
internal sealed class PlaylistConfigDialog : Form
{
    static readonly (string Label, int Height)[] VideoHeights =
    [
        ("En iyi", 0), ("2160p (4K)", 2160), ("1440p (2K)", 1440), ("1080p", 1080),
        ("720p", 720), ("480p", 480), ("360p", 360), ("240p", 240), ("144p", 144),
    ];
    static readonly string[] AudioFormats = ["best", "m4a", "mp3", "opus", "flac"];

    readonly SyncProfile _profile;
    readonly TextBox _target = new() { Dock = DockStyle.Fill };
    readonly RadioButton _kindMusic = new() { Text = Strings.MediaKindMusic, AutoSize = true, Margin = new Padding(8, 4, 12, 4) };
    readonly RadioButton _kindVideo = new() { Text = Strings.MediaKindVideo, AutoSize = true, Margin = new Padding(8, 4, 12, 4) };

    readonly GroupBox _musicGroup = new() { Text = Strings.MediaKindMusic, Dock = DockStyle.Top, AutoSize = true };
    readonly RadioButton _musicBest = new() { Text = Strings.QualityBest, AutoSize = true, Margin = new Padding(8, 4, 8, 2) };
    readonly RadioButton _musicCustom = new() { Text = Strings.QualityCustom, AutoSize = true, Margin = new Padding(8, 2, 8, 2) };
    readonly RadioButton _musicWorst = new() { Text = Strings.QualityWorst, AutoSize = true, Margin = new Padding(8, 2, 8, 2) };
    readonly ComboBox _audioFormat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100, Margin = new Padding(24, 2, 8, 2) };
    readonly NumericUpDown _audioBitrate = new() { Minimum = 0, Maximum = 1024, Increment = 32, Width = 80, Margin = new Padding(4, 2, 8, 2) };
    readonly CheckBox _codec2 = new() { Text = Strings.QualityConvertCodec2, AutoSize = true, Margin = new Padding(24, 2, 8, 4) };

    readonly GroupBox _videoGroup = new() { Text = Strings.MediaKindVideo, Dock = DockStyle.Top, AutoSize = true };
    readonly ComboBox _videoHeight = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Margin = new Padding(8, 4, 8, 4) };

    readonly CheckBox _embedThumb = new() { Text = "Kapak göm (thumbnail)", AutoSize = true, Margin = new Padding(8, 2, 8, 2) };
    readonly CheckBox _embedMeta = new() { Text = "Bilgileri göm (metadata)", AutoSize = true, Margin = new Padding(8, 2, 8, 2) };

    public SyncProfile Result => _profile;

    public PlaylistConfigDialog(SyncProfile profile)
    {
        _profile = profile;
        Text = profile.PlaylistTitle;
        Font = new Font("Segoe UI", 9f);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(560, 540);
        MinimumSize = new Size(480, 460);

        BuildLayout();
        LoadFrom(profile);
        WireToggles();
        UpdateEnabledState();
    }

    void BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var content = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };

        // Target folder row.
        var targetRow = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, RowCount = 2, AutoSize = true, Width = 520 };
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        targetRow.Controls.Add(Ui.Label(Strings.PlaylistsColTarget), 0, 0);
        targetRow.SetColumnSpan(targetRow.GetControlFromPosition(0, 0)!, 2);
        targetRow.Controls.Add(_target, 0, 1);
        targetRow.Controls.Add(Ui.Button(Strings.BtnBrowse, (_, _) => PickTarget()), 1, 1);
        content.Controls.Add(targetRow);

        // Media kind.
        var kindGroup = new GroupBox { Text = Strings.PlaylistsColMode, Dock = DockStyle.Top, AutoSize = true };
        var kindFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        kindFlow.Controls.Add(_kindMusic);
        kindFlow.Controls.Add(_kindVideo);
        kindGroup.Controls.Add(kindFlow);
        content.Controls.Add(kindGroup);

        // Music options.
        var musicFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        musicFlow.Controls.Add(_musicBest);
        musicFlow.Controls.Add(_musicCustom);
        var customRow = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
        _audioFormat.Items.AddRange([.. AudioFormats.Cast<object>()]);
        customRow.Controls.Add(_audioFormat);
        customRow.Controls.Add(Ui.Label("kbps (0=en iyi):"));
        customRow.Controls.Add(_audioBitrate);
        musicFlow.Controls.Add(customRow);
        musicFlow.Controls.Add(_musicWorst);
        musicFlow.Controls.Add(_codec2);
        _musicGroup.Controls.Add(musicFlow);
        content.Controls.Add(_musicGroup);

        // Video options.
        var videoFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        videoFlow.Controls.Add(Ui.Label(Strings.QualityVideoLabel));
        _videoHeight.Items.AddRange([.. VideoHeights.Select(v => (object)v.Label)]);
        videoFlow.Controls.Add(_videoHeight);
        _videoGroup.Controls.Add(videoFlow);
        content.Controls.Add(_videoGroup);

        // Embedding.
        var embedGroup = new GroupBox { Text = "Gömme", Dock = DockStyle.Top, AutoSize = true };
        var embedFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        embedFlow.Controls.Add(_embedThumb);
        embedFlow.Controls.Add(_embedMeta);
        embedGroup.Controls.Add(embedFlow);
        content.Controls.Add(embedGroup);

        // Buttons.
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var ok = Ui.Button(Strings.BtnSave, (_, _) => Save(), primary: true);
        ok.DialogResult = DialogResult.None;
        var cancel = new Button { Text = Strings.BtnCancel, DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(4) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
        CancelButton = cancel;
    }

    void LoadFrom(SyncProfile profile)
    {
        var options = profile.Options;
        _target.Text = profile.TargetFolder;
        _kindMusic.Checked = options.Kind == MediaKind.Music;
        _kindVideo.Checked = options.Kind == MediaKind.Video;

        _musicBest.Checked = options.MusicTier == MusicQualityTier.Best;
        _musicCustom.Checked = options.MusicTier == MusicQualityTier.Custom;
        _musicWorst.Checked = options.MusicTier == MusicQualityTier.Worst;
        _audioFormat.SelectedItem = AudioFormats.Contains(options.CustomAudioFormat) ? options.CustomAudioFormat : "m4a";
        _audioBitrate.Value = Math.Clamp(options.CustomAudioBitrateKbps, 0, 1024);
        _codec2.Checked = options.ConvertWorstToCodec2;

        int heightIndex = Array.FindIndex(VideoHeights, v => v.Height == options.VideoMaxHeight);
        _videoHeight.SelectedIndex = heightIndex >= 0 ? heightIndex : Array.FindIndex(VideoHeights, v => v.Height == 1080);

        _embedThumb.Checked = options.EmbedThumbnail;
        _embedMeta.Checked = options.EmbedMetadata;
    }

    void WireToggles()
    {
        _kindMusic.CheckedChanged += (_, _) => UpdateEnabledState();
        _kindVideo.CheckedChanged += (_, _) => UpdateEnabledState();
        _musicBest.CheckedChanged += (_, _) => UpdateEnabledState();
        _musicCustom.CheckedChanged += (_, _) => UpdateEnabledState();
        _musicWorst.CheckedChanged += (_, _) => UpdateEnabledState();
    }

    void UpdateEnabledState()
    {
        _musicGroup.Visible = _kindMusic.Checked;
        _videoGroup.Visible = _kindVideo.Checked;
        _audioFormat.Enabled = _audioBitrate.Enabled = _musicCustom.Checked;
        _codec2.Enabled = _musicWorst.Checked;
    }

    void PickTarget()
    {
        using var dialog = new FolderBrowserDialog { Description = Strings.PlaylistsColTarget };
        if (Directory.Exists(_target.Text)) dialog.SelectedPath = _target.Text;
        if (dialog.ShowDialog(this) == DialogResult.OK) _target.Text = dialog.SelectedPath;
    }

    void Save()
    {
        if (string.IsNullOrWhiteSpace(_target.Text))
        {
            MessageBox.Show(Strings.PlaylistsColTarget, AppConstants.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _profile.TargetFolder = _target.Text.Trim();
        var options = _profile.Options;
        options.Kind = _kindVideo.Checked ? MediaKind.Video : MediaKind.Music;
        options.MusicTier = _musicWorst.Checked ? MusicQualityTier.Worst : _musicCustom.Checked ? MusicQualityTier.Custom : MusicQualityTier.Best;
        options.CustomAudioFormat = _audioFormat.SelectedItem as string ?? "m4a";
        options.CustomAudioBitrateKbps = (int)_audioBitrate.Value;
        options.ConvertWorstToCodec2 = _codec2.Checked;
        options.VideoMaxHeight = _videoHeight.SelectedIndex >= 0 ? VideoHeights[_videoHeight.SelectedIndex].Height : 1080;
        options.EmbedThumbnail = _embedThumb.Checked;
        options.EmbedMetadata = _embedMeta.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }
}
