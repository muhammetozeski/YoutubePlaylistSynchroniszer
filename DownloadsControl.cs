namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// A live progress view for a sync run: per-video rows plus an overall bar and a phase line. It is a
/// passive <see cref="ISyncObserver"/> — the run is driven by <see cref="MainForm"/>, which feeds one of
/// these per playlist (its own tab) and one aggregate (the Downloads tab). Marshals all engine callbacks
/// onto the UI thread. The Cancel button raises <see cref="CancelRequested"/>.
/// </summary>
internal sealed class DownloadsControl : UserControl, ISyncObserver
{
    /// <summary>Raised when the user clicks Cancel (MainForm cancels the run's token).</summary>
    public event Action? CancelRequested;

    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
    };
    readonly ProgressBar _overall = new() { Dock = DockStyle.Fill, Height = 18, Minimum = 0, Maximum = 1 };
    readonly Label _phase = Ui.Label("");
    readonly Button _cancelButton;
    readonly Dictionary<string, int> _rowByVideo = [];

    int _completed, _toDownload;

    public DownloadsControl()
    {
        Dock = DockStyle.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = Strings.DownloadsColItem, FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = Strings.DownloadsColStatus, FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "progress", HeaderText = Strings.DownloadsColProgress, FillWeight = 12 });

        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.DownloadsCtxOpenVideo, null, (_, _) => OpenVideo(_grid.CurrentRow));
        menu.Items.Add(Strings.CtxCopyLink, null, (_, _) => CopyVideoLink(_grid.CurrentRow));
        _grid.ContextMenuStrip = menu;
        _grid.CellMouseDown += (_, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
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

    /// <summary>Lists the whole queue up front (one row per missing video, "queued"), before downloads start.</summary>
    public void OnQueued(IReadOnlyList<PlaylistVideo> queued) => Post(() =>
    {
        foreach (var video in queued)
        {
            if (_rowByVideo.ContainsKey(video.Id)) continue;
            int row = _grid.Rows.Add(video.Title, Strings.DownloadsStatusQueued, "");
            _grid.Rows[row].Tag = video.Id;
            _rowByVideo[video.Id] = row;
        }
    });

    public void OnItemStarted(string videoId, string title, int index, int total) => Post(() =>
    {
        if (!_rowByVideo.TryGetValue(videoId, out int row))
        {
            row = _grid.Rows.Add(title, Strings.DownloadsStatusDownloading, "0%");
            _grid.Rows[row].Tag = videoId;
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
        if (_rowByVideo.TryGetValue(videoId, out int row))
            _grid.Rows[row].Cells["progress"].Value = $"{percent:F0}%";
    });

    public void OnItemFinished(string videoId, DownloadOutcome outcome, string? message) => Post(() =>
    {
        if (_rowByVideo.TryGetValue(videoId, out int row))
        {
            _grid.Rows[row].Cells["status"].Value = outcome switch
            {
                DownloadOutcome.Downloaded => Strings.DownloadsStatusDone,
                DownloadOutcome.SkippedLive => Strings.DownloadsStatusSkippedLive,
                _ => string.Format(Strings.DownloadsStatusFailedFormat, message ?? ""),
            };
            if (outcome == DownloadOutcome.Downloaded) _grid.Rows[row].Cells["progress"].Value = "100%";
        }
        _completed++;
        _overall.Value = Math.Min(_completed, _overall.Maximum);
    });

    void OpenVideo(DataGridViewRow? row)
    {
        if (row?.Tag is string videoId) OpenUrlInBrowser(AppConstants.ShortVideoUrlBase + videoId);
    }

    void CopyVideoLink(DataGridViewRow? row)
    {
        if (row?.Tag is not string videoId) return;
        try { Clipboard.SetText(AppConstants.ShortVideoUrlBase + videoId); }
        catch (Exception ex) { Log("Copy video link failed: " + ex.Message, LogLevel.Warning); }
    }

    void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch { /* handle torn down */ }
    }
}
