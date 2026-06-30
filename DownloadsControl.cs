namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The Downloads tab: drives a sync run and shows live per-video progress. It implements
/// <see cref="ISyncObserver"/> and marshals every engine callback onto the UI thread. The run is wrapped
/// by <see cref="Resilience.GuardAsync"/>, so a failure offers the user a retry.
/// </summary>
internal sealed class DownloadsControl : UserControl, ISyncObserver
{
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

    CancellationTokenSource? _cancellation;
    bool _running;
    int _completed, _toDownload;

    public bool IsRunning => _running;

    public DownloadsControl()
    {
        Dock = DockStyle.Fill;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = Strings.DownloadsColItem, FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = Strings.DownloadsColStatus, FillWeight = 28 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "progress", HeaderText = Strings.DownloadsColProgress, FillWeight = 12 });

        var toolbar = Ui.Toolbar();
        _cancelButton = Ui.Button(Strings.DownloadsCancel, (_, _) => _cancellation?.Cancel());
        _cancelButton.Enabled = false;
        toolbar.Controls.Add(_cancelButton);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, ColumnCount = 1, RowCount = 2, AutoSize = true, Padding = new Padding(0, 4, 0, 0) };
        bottom.Controls.Add(_overall, 0, 0);
        bottom.Controls.Add(_phase, 0, 1);

        Controls.Add(_grid);
        Controls.Add(bottom);
        Controls.Add(toolbar);
    }

    /// <summary>Starts (or refuses, if already running) a sync over the given profiles.</summary>
    public async void Run(IReadOnlyList<SyncProfile> profiles)
    {
        if (_running) return;
        _running = true;
        _grid.Rows.Clear();
        _rowByVideo.Clear();
        _completed = 0;
        _toDownload = 0;
        _overall.Value = 0;
        _overall.Maximum = 1;
        _cancelButton.Enabled = true;
        _cancellation = new CancellationTokenSource();

        try
        {
            await Resilience.GuardAsync("GUI sync run", async () =>
            {
                if (Settings.AutoUpdateYtDlp.Value) await YtDlpManager.TryUpdateAsync(_cancellation.Token);
                var summary = await SyncEngine.SyncAllAsync(profiles, this, _cancellation.Token);
                SetPhase(string.Format(Strings.DownloadsSummaryFormat, summary.Downloaded, summary.SkippedLive, summary.Failed, summary.AlreadyPresent));
            });
        }
        finally
        {
            _cancelButton.Enabled = false;
            _cancellation?.Dispose();
            _cancellation = null;
            _running = false;
        }
    }

    // ---- ISyncObserver (engine threads → UI thread) ----

    public void OnPhase(string message) => SetPhase(message);

    public void OnPlaylistScanned(int playlistTotal, int alreadyPresent, int toDownload) => Post(() =>
    {
        _toDownload += toDownload;
        _overall.Maximum = Math.Max(1, _toDownload);
    });

    public void OnItemStarted(string videoId, string title, int index, int total) => Post(() =>
    {
        if (_rowByVideo.ContainsKey(videoId)) return;
        int row = _grid.Rows.Add(title, Strings.DownloadsStatusDownloading, "0%");
        _rowByVideo[videoId] = row;
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

    void SetPhase(string message) => Post(() => _phase.Text = message);

    void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch { /* handle torn down */ }
    }
}
