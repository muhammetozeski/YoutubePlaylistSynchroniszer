namespace YoutubePlaylistSynchroniszer;

/// <summary>A tiny modal text-input dialog (WinForms has no built-in one), used for pasting a refresh token.</summary>
internal static class Prompt
{
    /// <summary>Shows a single-line input. Returns the trimmed text, or null if cancelled.</summary>
    public static string? ForText(string title, string labelText, string defaultValue = "")
    {
        using var form = new Form
        {
            Text = title,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 140),
            Font = new Font("Segoe UI", 9f),
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = new Label { Text = labelText, AutoSize = true, Margin = new Padding(0, 0, 0, 6) };
        var box = new TextBox { Text = defaultValue, Dock = DockStyle.Top };

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        var ok = new Button { Text = Strings.BtnOk, DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(4) };
        var cancel = new Button { Text = Strings.BtnCancel, DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(4) };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(box, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        form.Controls.Add(layout);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        Theme.Apply(form);
        return form.ShowDialog() == DialogResult.OK ? box.Text.Trim() : null;
    }
}

/// <summary>A Yes/No confirmation with a "don't ask again" checkbox.</summary>
internal static class Confirm
{
    /// <summary>Returns whether the user confirmed and whether they ticked "don't ask again".</summary>
    public static (bool Confirmed, bool DontAskAgain) Ask(string message, IWin32Window? owner = null)
    {
        using var form = new Form
        {
            Text = AppConstants.AppTitle,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(440, 150),
            Font = new Font("Segoe UI", 9f),
        };

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label { Text = message, AutoSize = true, MaximumSize = new Size(410, 0) };
        var dontAsk = new CheckBox { Text = Strings.DontAskAgain, AutoSize = true, Margin = new Padding(0, 8, 0, 0) };

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        var yes = new Button { Text = Strings.BtnOk, DialogResult = DialogResult.Yes, AutoSize = true, Margin = new Padding(4) };
        var no = new Button { Text = Strings.BtnCancel, DialogResult = DialogResult.No, AutoSize = true, Margin = new Padding(4) };
        buttons.Controls.Add(yes);
        buttons.Controls.Add(no);

        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(dontAsk, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        form.Controls.Add(layout);
        form.AcceptButton = yes;
        form.CancelButton = no;

        Theme.Apply(form);
        bool confirmed = form.ShowDialog(owner) == DialogResult.Yes;
        return (confirmed, dontAsk.Checked);
    }
}
