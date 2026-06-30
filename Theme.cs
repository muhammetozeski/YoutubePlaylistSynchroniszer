using System.Runtime.InteropServices;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// Central light/dark palette and a recursive applier. Colors live here only, so re-skinning the whole
/// app is a one-place edit. <see cref="ApplyToWindow"/> also flips the native title bar to dark mode.
/// </summary>
internal static partial class Theme
{
    public static bool IsDark => Settings.DarkTheme.Value;

    public static Color Back => IsDark ? Color.FromArgb(32, 32, 36) : SystemColors.Control;
    public static Color Surface => IsDark ? Color.FromArgb(43, 43, 48) : SystemColors.Window;
    public static Color Fore => IsDark ? Color.FromArgb(236, 236, 236) : SystemColors.ControlText;
    public static Color GridBack => IsDark ? Color.FromArgb(28, 28, 32) : SystemColors.Window;
    public static Color HeaderBack => IsDark ? Color.FromArgb(52, 52, 58) : SystemColors.Control;
    public static Color GridLines => IsDark ? Color.FromArgb(60, 60, 66) : SystemColors.ControlDark;
    public static Color SelectionBack => Color.FromArgb(0xB4, 0x2B, 0x27);
    public static Color ButtonBack => IsDark ? Color.FromArgb(56, 56, 62) : SystemColors.Control;
    public static Color ButtonBorder => IsDark ? Color.FromArgb(86, 86, 94) : SystemColors.ControlDark;

    const int DwmwaUseImmersiveDarkMode = 20;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>Applies the palette to a form (including the native title bar) and all its controls.</summary>
    public static void ApplyToWindow(Form form)
    {
        try
        {
            int dark = IsDark ? 1 : 0;
            if (form.IsHandleCreated) DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        }
        catch (Exception ex) { Log("Dark title bar set failed: " + ex.Message, LogLevel.Warning); }

        Apply(form);
        form.Invalidate(true);
    }

    /// <summary>Recursively themes a control tree.</summary>
    public static void Apply(Control control)
    {
        switch (control)
        {
            case DataGridView grid:
                grid.EnableHeadersVisualStyles = false;
                grid.BackgroundColor = GridBack;
                grid.GridColor = GridLines;
                grid.ColumnHeadersDefaultCellStyle.BackColor = HeaderBack;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Fore;
                grid.DefaultCellStyle.BackColor = Surface;
                grid.DefaultCellStyle.ForeColor = Fore;
                grid.DefaultCellStyle.SelectionBackColor = SelectionBack;
                grid.DefaultCellStyle.SelectionForeColor = Color.White;
                break;
            case TextBox textBox:
                textBox.BackColor = Surface;
                textBox.ForeColor = Fore;
                break;
            case Button button:
                // Leave the accent (primary) buttons alone; theme the rest.
                if (button.BackColor != Ui.Accent)
                {
                    button.BackColor = ButtonBack;
                    button.ForeColor = Fore;
                    button.FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                    if (IsDark) button.FlatAppearance.BorderColor = ButtonBorder;
                }
                break;
            default:
                control.BackColor = control is ComboBox or NumericUpDown ? Surface : Back;
                control.ForeColor = Fore;
                break;
        }

        foreach (Control child in control.Controls) Apply(child);
    }
}
