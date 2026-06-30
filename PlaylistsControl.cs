namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The Playlists tab: loads the account's playlists, lets the user tick which to sync, set each one's
/// target folder + quality, view a playlist's contents, and start a sync. Selection supports multi-row
/// (right-click → check/uncheck) and bulk select-all / clear-all (with a confirm that can be silenced).
/// The ticked state + per-playlist setup persist as <see cref="SyncProfile"/>s, which the headless
/// <c>--sync</c> run replays.
/// </summary>
internal sealed class PlaylistsControl : UserControl
{
    /// <summary>Raised when the user starts a sync; the shell switches to the per-playlist progress tabs.</summary>
    public event Action<IReadOnlyList<SyncProfile>>? SyncRequested;

    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        MultiSelect = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
    };
    readonly Label _status = Ui.Label("");
    readonly Button _loadButton;
    bool _suppressToggleHandler;
    int _fillGeneration; // bumped on each reload so a stale background fill stops updating

    public PlaylistsControl()
    {
        Dock = DockStyle.Fill;
        BuildColumns();
        BuildContextMenu();

        _grid.CellContentClick += OnCellContentClick;
        _grid.CellDoubleClick += OnCellDoubleClick;
        _grid.CellMouseDown += OnCellMouseDown;
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += OnSelectionToggled;

        var toolbar = Ui.Toolbar();
        _loadButton = Ui.Button(Strings.PlaylistsLoad, (_, _) => LoadPlaylists());
        toolbar.Controls.Add(_loadButton);
        toolbar.Controls.Add(Ui.Button(Strings.PlaylistsSelectAll, (_, _) => BulkSet(true)));
        toolbar.Controls.Add(Ui.Button(Strings.PlaylistsClearAll, (_, _) => BulkSet(false)));
        toolbar.Controls.Add(Ui.Button(Strings.PlaylistsSyncSelected, (_, _) => SyncSelected(), primary: true));
        toolbar.Controls.Add(_status);

        Controls.Add(_grid);
        Controls.Add(toolbar);
        NotifyAuthChanged();
    }

    void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "select", HeaderText = "✓", FillWeight = 5 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = Strings.PlaylistsColTitle, FillWeight = 28, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "count", HeaderText = Strings.PlaylistsColCount, FillWeight = 7, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "created", HeaderText = Strings.PlaylistsColCreated, FillWeight = 10, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "lastAdded", HeaderText = Strings.PlaylistsColLastAdded, FillWeight = 14, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mode", HeaderText = Strings.PlaylistsColMode, FillWeight = 7, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "quality", HeaderText = "Kalite", FillWeight = 12, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "target", HeaderText = Strings.PlaylistsColTarget, FillWeight = 15, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "configure", HeaderText = "", Text = Strings.PlaylistsConfigureColumn, UseColumnTextForButtonValue = true, FillWeight = 10 });
    }

    void BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(Strings.CtxCheckSelected, null, (_, _) => SetRowsChecked(SelectedProfileRows(), true));
        menu.Items.Add(Strings.CtxUncheckSelected, null, (_, _) => SetRowsChecked(SelectedProfileRows(), false));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Strings.CtxViewContents, null, (_, _) => ViewContents(_grid.CurrentRow));
        menu.Items.Add(Strings.CtxOpenInBrowser, null, (_, _) => OpenInBrowser(_grid.CurrentRow));
        _grid.ContextMenuStrip = menu;
    }

    /// <summary>Called by the shell after the auth state changes; refreshes the status line.</summary>
    public void NotifyAuthChanged() =>
        _status.Text = CredentialStore.IsAuthorized ? "" : Strings.PlaylistsNeedAuth;

    async void LoadPlaylists()
    {
        if (!CredentialStore.IsAuthorized) { _status.Text = Strings.PlaylistsNeedAuth; return; }
        _loadButton.Enabled = false;
        _status.Text = Strings.PlaylistsLoadingFormat;

        await Resilience.GuardAsync("Load my playlists", async () =>
        {
            var playlists = await YouTubeApiClient.ListMyPlaylistsAsync();
            PopulateGrid(playlists);
            _status.Text = string.Format(Strings.PlaylistsCountFormat, playlists.Count);
        });

        _loadButton.Enabled = true;
    }

    void PopulateGrid(IReadOnlyList<YouTubePlaylist> playlists)
    {
        _grid.Rows.Clear();
        int generation = ++_fillGeneration; // invalidate any in-flight last-added fill
        foreach (var playlist in playlists)
        {
            var stored = SyncProfileStore.Get(playlist.Id);
            var profile = stored ?? new SyncProfile { PlaylistId = playlist.Id };
            if (profile.PlaylistTitle != playlist.Title)
            {
                profile.PlaylistTitle = playlist.Title; // keep the cached title fresh
                if (stored is not null) SyncProfileStore.Upsert(profile);
            }

            int rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Tag = profile;
            row.Cells["select"].Value = profile.EnabledForBackgroundSync;
            row.Cells["title"].Value = playlist.Title;
            row.Cells["count"].Value = playlist.ItemCount;
            row.Cells["created"].Value = playlist.CreatedAt?.ToLocalTime().ToString("yyyy-MM-dd") ?? "";
            row.Cells["lastAdded"].Value = "…"; // filled in the background (one API call per playlist)
            FillProfileCells(row, profile);
        }

        _ = FillLastAddedDatesAsync(generation);
    }

    /// <summary>Fills the "last added" column in the background: for each playlist, the newest video's added
    /// date (max of its items' added dates). Bounded concurrency; a reload bumps the generation to stop a
    /// stale fill from writing into the new grid.</summary>
    async Task FillLastAddedDatesAsync(int generation)
    {
        var targets = _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is SyncProfile)
            .Select(r => (row: r, id: ((SyncProfile)r.Tag!).PlaylistId))
            .ToList();

        using var gate = new SemaphoreSlim(4);
        var tasks = targets.Select(async target =>
        {
            await gate.WaitAsync();
            try
            {
                if (generation != _fillGeneration) return;
                DateTime? lastAdded = null;
                try
                {
                    var videos = await YouTubeApiClient.ListPlaylistVideosAsync(target.id);
                    var dates = videos.Where(v => v.AddedAt.HasValue).Select(v => v.AddedAt!.Value).ToList();
                    if (dates.Count > 0) lastAdded = dates.Max();
                }
                catch (Exception ex) { Log("Last-added fetch failed for " + target.id + ": " + ex.Message, LogLevel.Warning); }

                string text = lastAdded?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";
                if (generation != _fillGeneration || IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke(() => { if (generation == _fillGeneration && target.row.Index >= 0) target.row.Cells["lastAdded"].Value = text; }); }
                catch { /* handle torn down */ }
            }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);
    }

    static void FillProfileCells(DataGridViewRow row, SyncProfile profile)
    {
        row.Cells["mode"].Value = profile.Options.Kind == MediaKind.Video ? Strings.MediaKindVideo : Strings.MediaKindMusic;
        row.Cells["quality"].Value = profile.Options.Describe();
        row.Cells["target"].Value = string.IsNullOrWhiteSpace(profile.TargetFolder) ? "—" : profile.TargetFolder;
    }

    void OnCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "configure") return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not SyncProfile profile) return;

        using var dialog = new PlaylistConfigDialog(profile);
        Theme.Apply(dialog);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        profile.EnabledForBackgroundSync = (bool)(row.Cells["select"].Value ?? false);
        SyncProfileStore.Upsert(profile);
        FillProfileCells(row, profile);
    }

    void OnCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        // Double-clicking the configure button column is handled by its own click; ignore it here.
        if (_grid.Columns[e.ColumnIndex].Name is "configure" or "select") return;
        ViewContents(_grid.Rows[e.RowIndex]);
    }

    // Right-click selects the row under the cursor when it isn't already part of the selection.
    void OnCellMouseDown(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
        if (!_grid.Rows[e.RowIndex].Selected)
        {
            _grid.ClearSelection();
            _grid.Rows[e.RowIndex].Selected = true;
        }
    }

    void ViewContents(DataGridViewRow? row)
    {
        if (row?.Tag is not SyncProfile profile) return;
        using var dialog = new PlaylistContentsDialog(new YouTubePlaylist(profile.PlaylistId, profile.PlaylistTitle, 0));
        dialog.ShowDialog(this);
    }

    void OpenInBrowser(DataGridViewRow? row)
    {
        if (row?.Tag is not SyncProfile profile) return;
        OpenUrlInBrowser("https://www.youtube.com/playlist?list=" + profile.PlaylistId);
    }

    // A single user click on a checkbox; bulk/context updates set values with the handler suppressed.
    void OnSelectionToggled(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressToggleHandler || e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "select") return;
        SetRowsChecked([_grid.Rows[e.RowIndex]], (bool)(_grid.Rows[e.RowIndex].Cells["select"].Value ?? false));
    }

    IEnumerable<DataGridViewRow> SelectedProfileRows() =>
        _grid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Tag is SyncProfile);

    void BulkSet(bool value)
    {
        if (_grid.Rows.Count == 0) return;
        if (Settings.ConfirmBulkSelect.Value)
        {
            string message = value ? string.Format(Strings.ConfirmSelectAllFormat, _grid.Rows.Count) : Strings.ConfirmClearAll;
            var (confirmed, dontAskAgain) = Confirm.Ask(message, FindForm());
            if (!confirmed) return;
            if (dontAskAgain) { Settings.ConfirmBulkSelect.Value = false; SettingsManager.SaveSettings(); }
        }
        SetRowsChecked(_grid.Rows.Cast<DataGridViewRow>(), value);
    }

    /// <summary>Sets the checkbox for the given rows and persists the change once (only for profiles that
    /// are configured or already stored — unconfigured ones keep an in-session flag without cluttering the
    /// store).</summary>
    void SetRowsChecked(IEnumerable<DataGridViewRow> rows, bool value)
    {
        var toPersist = new List<SyncProfile>();
        _suppressToggleHandler = true;
        try
        {
            foreach (var row in rows)
            {
                if (row.Tag is not SyncProfile profile) continue;
                row.Cells["select"].Value = value;
                profile.EnabledForBackgroundSync = value;
                if (profile.IsReadyToSync || SyncProfileStore.Get(profile.PlaylistId) is not null)
                    toPersist.Add(profile);
            }
        }
        finally { _suppressToggleHandler = false; }
        SyncProfileStore.UpsertMany(toPersist);
    }

    void SyncSelected()
    {
        var ready = new List<SyncProfile>();
        var needTarget = new List<string>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not SyncProfile profile || (bool)(row.Cells["select"].Value ?? false) == false) continue;
            if (profile.IsReadyToSync) ready.Add(profile);
            else needTarget.Add(profile.PlaylistTitle);
        }

        if (needTarget.Count > 0)
            NativeMessageBox.Warn(string.Format(Strings.PlaylistsNeedTargetFormat, string.Join("\n", needTarget)));
        if (ready.Count == 0)
        {
            if (needTarget.Count == 0) NativeMessageBox.Info(Strings.PlaylistsNothingSelected);
            return;
        }
        SyncRequested?.Invoke(ready);
    }
}
