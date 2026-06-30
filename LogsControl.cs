namespace YoutubePlaylistSynchroniszer;

/// <summary>The Logs tab: a live, read-only view of the operation log (fed by <see cref="LoggerHost"/>),
/// with copy / clear / open-folder actions. Capped so a long session can't grow the box unbounded.</summary>
internal sealed class LogsControl : UserControl
{
    const int MaxCharacters = 400_000;

    readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9f),
        BackColor = Color.FromArgb(24, 24, 28),
        ForeColor = Color.Gainsboro,
        BorderStyle = BorderStyle.None,
    };

    public LogsControl()
    {
        Dock = DockStyle.Fill;

        var toolbar = Ui.Toolbar();
        toolbar.Controls.Add(Ui.Button(Strings.LogsCopy, (_, _) => CopyLogs()));
        toolbar.Controls.Add(Ui.Button(Strings.LogsClear, (_, _) => { _log.Clear(); Logger.ClearAllLogs(); }));
        toolbar.Controls.Add(Ui.Button(Strings.LogsOpenFolder, (_, _) => OpenWithDefaultProgram(ConfigPathResolver.LogsFolder)));

        Controls.Add(_log);
        Controls.Add(toolbar);

        _log.AppendText(Logger.GetAllLogsText());
        LoggerHost.OnLogLine += AppendLine;
    }

    void CopyLogs()
    {
        try { if (_log.TextLength > 0) Clipboard.SetText(_log.Text); }
        catch (Exception ex) { Log("Copy logs failed: " + ex.Message, LogLevel.Warning); }
    }

    void AppendLine(string line)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                if (_log.TextLength > MaxCharacters) _log.Text = _log.Text[^(MaxCharacters / 2)..];
                _log.AppendText(line + "\n");
            });
        }
        catch { /* handle torn down between the check and the post */ }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) LoggerHost.OnLogLine -= AppendLine;
        base.Dispose(disposing);
    }
}
