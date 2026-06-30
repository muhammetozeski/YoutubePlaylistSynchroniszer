namespace YoutubePlaylistSynchroniszer;

/// <summary>Read-only view of a playlist's videos (index, title, when it was added, video id), for info.</summary>
internal sealed class PlaylistContentsDialog : Form
{
    readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BorderStyle = BorderStyle.None,
    };
    readonly Label _status = Ui.Label("");
    readonly string _playlistId;

    public PlaylistContentsDialog(YouTubePlaylist playlist)
    {
        _playlistId = playlist.Id;
        Text = string.Format(Strings.ContentsTitleFormat, playlist.Title);
        Font = new Font("Segoe UI", 9f);
        Icon = AppIcon.Shared;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 520);
        MinimumSize = new Size(480, 320);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "index", HeaderText = Strings.ContentsColIndex, FillWeight = 6 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = Strings.ContentsColTitle, FillWeight = 56 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "added", HeaderText = Strings.ContentsColAdded, FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "videoId", HeaderText = Strings.ContentsColVideoId, FillWeight = 16 });

        Controls.Add(_grid);
        Controls.Add(_status);
        _status.Dock = DockStyle.Bottom;

        Load += (_, _) => { Theme.ApplyToWindow(this); LoadContents(); };
    }

    async void LoadContents()
    {
        _status.Text = Strings.ContentsLoadingFormat;
        await Resilience.GuardAsync("View playlist contents", async () =>
        {
            var videos = await YouTubeApiClient.ListPlaylistVideosAsync(_playlistId);
            int index = 1;
            foreach (var video in videos)
                _grid.Rows.Add(index++, video.Title, video.AddedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "", video.Id);
            _status.Text = string.Format(Strings.ContentsCountFormat, videos.Count);
        });
    }
}
