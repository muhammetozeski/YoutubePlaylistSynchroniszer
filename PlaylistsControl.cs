namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// The Playlists tab: loads the account's playlists, lets the user tick which ones to sync, set each
/// one's target folder + quality (the "Ayarla…" button), and start a sync. The ticked state and the
/// per-playlist setup are persisted as <see cref="SyncProfile"/>s, which is exactly what the headless
/// <c>--sync</c> run replays.
/// </summary>
internal sealed class PlaylistsControl : UserControl
{
    /// <summary>Raised when the user starts a sync; the shell switches to the Downloads tab to run it.</summary>
    public event Action<IReadOnlyList<SyncProfile>>? SyncRequested;

    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
    };
    readonly Label _status = Ui.Label("");
    readonly Button _loadButton;

    public PlaylistsControl()
    {
        Dock = DockStyle.Fill;
        BuildColumns();

        _grid.CellContentClick += OnCellContentClick;
        _grid.CurrentCellDirtyStateChanged += (_, _) => { if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit); };
        _grid.CellValueChanged += OnSelectionToggled;

        var toolbar = Ui.Toolbar();
        _loadButton = Ui.Button(Strings.PlaylistsLoad, (_, _) => LoadPlaylists());
        toolbar.Controls.Add(_loadButton);
        toolbar.Controls.Add(Ui.Button(Strings.PlaylistsSyncSelected, (_, _) => SyncSelected(), primary: true));
        toolbar.Controls.Add(_status);

        Controls.Add(_grid);
        Controls.Add(toolbar);
        NotifyAuthChanged();
    }

    void BuildColumns()
    {
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { Name = "select", HeaderText = "✓", FillWeight = 6 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = Strings.PlaylistsColTitle, FillWeight = 32, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "count", HeaderText = Strings.PlaylistsColCount, FillWeight = 8, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "mode", HeaderText = Strings.PlaylistsColMode, FillWeight = 10, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "quality", HeaderText = "Kalite", FillWeight = 16, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "target", HeaderText = Strings.PlaylistsColTarget, FillWeight = 22, ReadOnly = true });
        _grid.Columns.Add(new DataGridViewButtonColumn { Name = "configure", HeaderText = "", Text = Strings.PlaylistsConfigureColumn, UseColumnTextForButtonValue = true, FillWeight = 12 });
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
        foreach (var playlist in playlists)
        {
            var stored = SyncProfileStore.Get(playlist.Id);
            var profile = stored ?? new SyncProfile { PlaylistId = playlist.Id };
            if (profile.PlaylistTitle != playlist.Title)
            {
                profile.PlaylistTitle = playlist.Title; // keep the cached title fresh
                if (stored is not null) SyncProfileStore.Upsert(profile); // persist the refreshed title
            }

            int rowIndex = _grid.Rows.Add();
            var row = _grid.Rows[rowIndex];
            row.Tag = profile;
            row.Cells["select"].Value = profile.EnabledForBackgroundSync;
            row.Cells["title"].Value = playlist.Title;
            row.Cells["count"].Value = playlist.ItemCount;
            FillProfileCells(row, profile);
        }
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
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        profile.EnabledForBackgroundSync = (bool)(row.Cells["select"].Value ?? false);
        SyncProfileStore.Upsert(profile);
        FillProfileCells(row, profile);
    }

    void OnSelectionToggled(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _grid.Columns[e.ColumnIndex].Name != "select") return;
        var row = _grid.Rows[e.RowIndex];
        if (row.Tag is not SyncProfile profile) return;

        profile.EnabledForBackgroundSync = (bool)(row.Cells["select"].Value ?? false);
        SyncProfileStore.Upsert(profile);
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
