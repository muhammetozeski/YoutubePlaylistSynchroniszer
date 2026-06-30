namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// A live progress view for a sync run: per-video rows plus an overall bar and a phase line. It is a
/// passive <see cref="ISyncObserver"/> — the run is driven by <see cref="MainForm"/>, which feeds one of
/// these per playlist (its own tab) and one aggregate (the Downloads tab). Marshals every engine callback
/// onto the UI thread. The grid has a rich right-click menu (open / copy / list / file actions).
/// </summary>
internal sealed class DownloadsControl : UserControl, ISyncObserver
{
    /// <summary>Per-row data carried in <see cref="DataGridViewRow.Tag"/>.</summary>
    sealed class DownloadRow(string videoId, string title)
    {
        public string VideoId { get; } = videoId;
        public string Title { get; set; } = title;
        public string TargetFolder { get; set; } = "";
        public string? FilePath { get; set; }
        public DownloadOutcome? Outcome { get; set; }
    }

    /// <summary>Raised when the user clicks Cancel (MainForm cancels the run's token).</summary>
    public event Action? CancelRequested;

    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        MultiSelect = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
    };
    readonly ProgressBar _overall = new() { Dock = DockStyle.Fill, Height = 18, Minimum = 0, Maximum = 1 };
    readonly Label _phase = Ui.Label("");
    readonly Button _cancelButton;
    readonly Dictionary<string, int> _rowByVideo = [];
    readonly List<ToolStripItem> _fileItems = [];

    int _completed, _toDownload;

    public DownloadsControl()
    {
        Dock = DockStyle.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = Strings.DownloadsColItem, FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = Strings.DownloadsColStatus, FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "progress", HeaderText = Strings.DownloadsColProgress, FillWeight = 12 });

        BuildContextMenu();
        _grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            if (!_grid.Rows[e.RowIndex].Selected) { _grid.ClearSelection(); _grid.Rows[e.RowIndex].Selected = true; }
            _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[0];
        };

        var toolbar = Ui.Toolbar();
        _cancelButton = Ui.Button(Strings.DownloadsCancel, (_, _) => CancelRequested?.Invoke());
        _cancelButton.Enabled = false;
        toolbar.Controls.Add(_cancelButton);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 1, RowCount = 2, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
        bottom.Controls.Add(_overall, 0, 0);
        bottom.Controls.Add(_phase, 0, 1);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(toolbar);
    }

    void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();

        var open = new ToolStripMenuItem(Strings.DlMenuOpen);
        open.DropDownItems.Add(Strings.DownloadsCtxOpenVideo, null, (_, _) => OnCurrent(r => OpenUrlInBrowser(Short(r.VideoId))));
        open.DropDownItems.Add(Strings.DlOpenThumbnail, null, (_, _) => OnCurrent(r => OpenUrlInBrowser(Thumb(r.VideoId))));
        _fileItems.Add(open.DropDownItems.Add(Strings.DlOpenFile, null, (_, _) => OnCurrent(r => { if (FileExists(r)) OpenWithDefaultProgram(r.FilePath!); })));
        _fileItems.Add(open.DropDownItems.Add(Strings.DlOpenFileLocation, null, (_, _) => OnCurrent(r => { if (FileExists(r)) RevealInExplorer(r.FilePath!); })));
        open.DropDownItems.Add(Strings.DlOpenTargetFolder, null, (_, _) => OnCurrent(r => { if (Directory.Exists(r.TargetFolder)) OpenWithDefaultProgram(r.TargetFolder); }));

        var copy = new ToolStripMenuItem(Strings.DlMenuCopy);
        copy.DropDownItems.Add(Strings.DlCopyShortLink, null, (_, _) => OnCurrent(r => Copy(Short(r.VideoId))));
        copy.DropDownItems.Add(Strings.DlCopyLongLink, null, (_, _) => OnCurrent(r => Copy(Watch(r.VideoId))));
        copy.DropDownItems.Add(Strings.DlCopyEmbedLink, null, (_, _) => OnCurrent(r => Copy(Embed(r.VideoId))));
        copy.DropDownItems.Add(Strings.DlCopyVideoId, null, (_, _) => OnCurrent(r => Copy(r.VideoId)));
        copy.DropDownItems.Add(Strings.DlCopyTitle, null, (_, _) => OnCurrent(r => Copy(r.Title)));
        copy.DropDownItems.Add(Strings.DlCopyTitleAndLink, null, (_, _) => OnCurrent(r => Copy($"{r.Title} - {Short(r.VideoId)}")));
        _fileItems.Add(copy.DropDownItems.Add(Strings.DlCopyFilePath, null, (_, _) => OnCurrent(r => Copy(r.FilePath ?? ""))));
        copy.DropDownItems.Add(Strings.DlCopyStatus, null, (_, _) => OnCurrent(r => Copy(StatusText(r.VideoId))));
        copy.DropDownItems.Add(Strings.DlCopyYtDlpCommand, null, (_, _) => OnCurrent(r => Copy(YtDlpManager.BuildDiagnosticCommand(r.VideoId))));

        var list = new ToolStripMenuItem(Strings.DlMenuList);
        list.DropDownItems.Add(Strings.DlCopySelectedLinks, null, (_, _) => Copy(LinksOf(SelectedRowTags())));
        list.DropDownItems.Add(Strings.DlCopyAllLinks, null, (_, _) => Copy(LinksOf(AllRowTags())));
        list.DropDownItems.Add(Strings.DlCopyFailedLinks, null, (_, _) => Copy(LinksOf(AllRowTags().Where(r => r.Outcome == DownloadOutcome.Failed))));
        list.DropDownItems.Add(Strings.DlRemoveRow, null, (_, _) => RemoveSelectedRows());
        list.DropDownItems.Add(Strings.DlClearCompleted, null, (_, _) => ClearCompleted());

        menu.Items.Add(open);
        menu.Items.Add(copy);
        menu.Items.Add(list);
        menu.Items.Add(new ToolStripSeparator());
        _fileItems.Add(menu.Items.Add(Strings.DlDeleteFile, null, (_, _) => OnCurrent(DeleteFile)));

        // Enable file-only actions only when the current row actually has a downloaded file.
        menu.Opening += (_, _) =>
        {
            bool hasFile = _grid.CurrentRow?.Tag is DownloadRow row && FileExists(row);
            foreach (var item in _fileItems) item.Enabled = hasFile;
        };
        _grid.ContextMenuStrip = menu;
    }

    /// <summary>Clears the view and arms the Cancel button for a new run.</summary>
    public void PrepareForRun() => Post(() =>
    {
        _grid.Rows.Clear();
        _rowByVideo.Clear();
        _completed = 0;
        _toDownload = 0;
        _overall.Value = 0;
        _overall.Maximum = 1;
        _phase.Text = "";
        _cancelButton.Enabled = true;
    });

    /// <summary>Marks the run finished: shows the summary line and disables Cancel.</summary>
    public void Finish(string summary) => Post(() =>
    {
        _phase.Text = summary;
        _cancelButton.Enabled = false;
    });

    // ---- ISyncObserver (engine threads → UI thread) ----

    public void OnPhase(string message) => Post(() => _phase.Text = message);

    public void OnPlaylistScanned(int playlistTotal, int alreadyPresent, int toDownload) => Post(() =>
    {
        _toDownload += toDownload;
        _overall.Maximum = Math.Max(1, _toDownload);
    });

    public void OnQueued(IReadOnlyList<PlaylistVideo> queued, string targetFolder) => Post(() =>
    {
        foreach (var video in queued)
        {
            if (_rowByVideo.ContainsKey(video.Id)) continue;
            int row = _grid.Rows.Add(video.Title, Strings.DownloadsStatusQueued, "");
            _grid.Rows[row].Tag = new DownloadRow(video.Id, video.Title) { TargetFolder = targetFolder };
            _rowByVideo[video.Id] = row;
        }
    });

    public void OnItemStarted(string videoId, string title, int index, int total) => Post(() =>
    {
        // Re-add if the stored index is missing or stale (a row may have been removed from the list).
        if (!_rowByVideo.TryGetValue(videoId, out int row) || row >= _grid.Rows.Count)
        {
            row = _grid.Rows.Add(title, Strings.DownloadsStatusDownloading, "0%");
            _grid.Rows[row].Tag = new DownloadRow(videoId, title);
            _rowByVideo[videoId] = row;
        }
        else
        {
            _grid.Rows[row].Cells["status"].Value = Strings.DownloadsStatusDownloading;
        }
        _grid.FirstDisplayedScrollingRowIndex = row;
    });

    public void OnItemProgress(string videoId, double percent) => Post(() =>
    {
        if (_rowByVideo.TryGetValue(videoId, out int row) && row < _grid.Rows.Count)
            _grid.Rows[row].Cells["progress"].Value = $"{percent:F0}%";
    });

    public void OnItemFinished(string videoId, DownloadOutcome outcome, string? message, string? filePath) => Post(() =>
    {
        if (_rowByVideo.TryGetValue(videoId, out int row) && row < _grid.Rows.Count)
        {
            _grid.Rows[row].Cells["status"].Value = outcome switch
            {
                DownloadOutcome.Downloaded => Strings.DownloadsStatusDone,
                DownloadOutcome.SkippedLive => Strings.DownloadsStatusSkippedLive,
                _ => string.Format(Strings.DownloadsStatusFailedFormat, message ?? ""),
            };
            if (outcome == DownloadOutcome.Downloaded) _grid.Rows[row].Cells["progress"].Value = "100%";
            if (_grid.Rows[row].Tag is DownloadRow tag) { tag.Outcome = outcome; tag.FilePath = filePath; }
        }
        _completed++;
        _overall.Value = Math.Min(_completed, _overall.Maximum);
    });

    // ---- menu actions ----

    void OnCurrent(Action<DownloadRow> action)
    {
        if (_grid.CurrentRow?.Tag is DownloadRow row) action(row);
    }

    void DeleteFile(DownloadRow row)
    {
        if (!FileExists(row)) return;
        if (!NativeMessageBox.Confirm(string.Format(Strings.DlDeleteFileConfirmFormat, row.FilePath))) return;
        try
        {
            File.Delete(row.FilePath!);
            row.FilePath = null;
            if (_rowByVideo.TryGetValue(row.VideoId, out int index) && index < _grid.Rows.Count)
                _grid.Rows[index].Cells["status"].Value = Strings.DownloadsStatusDeleted;
        }
        catch (Exception ex) { NativeMessageBox.Error(ex.Message); }
    }

    void RemoveSelectedRows()
    {
        foreach (var row in _grid.SelectedRows.Cast<DataGridViewRow>().OrderByDescending(r => r.Index).ToList())
            _grid.Rows.Remove(row);
        RebuildIndex();
    }

    void ClearCompleted()
    {
        foreach (var row in _grid.Rows.Cast<DataGridViewRow>().Where(r => (r.Tag as DownloadRow)?.Outcome == DownloadOutcome.Downloaded).ToList())
            _grid.Rows.Remove(row);
        RebuildIndex();
    }

    void RebuildIndex()
    {
        _rowByVideo.Clear();
        for (int i = 0; i < _grid.Rows.Count; i++)
            if (_grid.Rows[i].Tag is DownloadRow tag) _rowByVideo[tag.VideoId] = i;
    }

    string StatusText(string videoId) =>
        _rowByVideo.TryGetValue(videoId, out int row) ? _grid.Rows[row].Cells["status"].Value?.ToString() ?? "" : "";

    IEnumerable<DownloadRow> AllRowTags() => _grid.Rows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<DownloadRow>();
    IEnumerable<DownloadRow> SelectedRowTags() => _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<DownloadRow>();

    static bool FileExists(DownloadRow row) => !string.IsNullOrEmpty(row.FilePath) && File.Exists(row.FilePath);
    static string LinksOf(IEnumerable<DownloadRow> rows) => string.Join(Environment.NewLine, rows.Select(r => Short(r.VideoId)));

    static string Short(string id) => AppConstants.ShortVideoUrlBase + id;
    static string Watch(string id) => "https://www.youtube.com/watch?v=" + id;
    static string Embed(string id) => "https://www.youtube.com/embed/" + id;
    static string Thumb(string id) => $"https://img.youtube.com/vi/{id}/maxresdefault.jpg";

    static void Copy(string text)
    {
        try { if (!string.IsNullOrEmpty(text)) Clipboard.SetText(text); }
        catch (Exception ex) { Log("Clipboard copy failed: " + ex.Message, LogLevel.Warning); }
    }

    void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch { /* handle torn down */ }
    }
}
