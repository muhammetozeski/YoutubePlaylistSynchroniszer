namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Small factory helpers so controls are built consistently in one place (fonts, spacing, accent color).
/// Changing the look of every button/header is a one-edit change here.
/// </summary>
internal static class Ui
{
    public static readonly Color Accent = Color.FromArgb(0xCC, 0x00, 0x00); // YouTube-ish red, used sparingly
    public static readonly Padding Gap = new(6);

    public static Label Header(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 13f, FontStyle.Bold),
        AutoSize = true,
        Margin = new Padding(2, 4, 2, 10),
    };

    public static Label Label(string text, bool muted = false) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(2, 6, 6, 6),
        ForeColor = muted ? SystemColors.GrayText : SystemColors.ControlText,
    };

    public static Button Button(string text, EventHandler onClick, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(4),
            Padding = new Padding(8, 4, 8, 4),
        };
        if (primary)
        {
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }
        button.Click += onClick;
        return button;
    }

    public static CheckBox Check(string text, bool value, EventHandler onChanged)
    {
        var check = new CheckBox { Text = text, Checked = value, AutoSize = true, Margin = new Padding(4, 6, 4, 6) };
        check.CheckedChanged += onChanged;
        return check;
    }

    /// <summary>A left-to-right, non-wrapping, auto-sizing button/label strip for toolbars.</summary>
    public static FlowLayoutPanel Toolbar() => new()
    {
        Dock = DockStyle.Top,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = new Padding(0),
        Padding = new Padding(0, 0, 0, 6),
    };
}
