using System.Runtime.InteropServices;
using YoutubePlaylistSynchroniszer;

// Native Win32 MessageBox wrapper (adapted from C:\E\KodlamaProjeleri\CSharp\TPMPass\NativeMessageBox.cs).
// Used for unexpected-exception dialogs and the retry prompts, which must work even with no WinForms
// message loop running (e.g. the headless --sync mode), so it calls user32 directly.
#pragma warning disable CA1050 // Declare types in namespaces
internal static partial class NativeMessageBox
#pragma warning restore CA1050
{
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    // Win32 MessageBox flags.
    const uint MB_OK = 0x0;
    const uint MB_YESNO = 0x4;
    const uint MB_ICONERROR = 0x10;
    const uint MB_ICONWARNING = 0x30;
    const uint MB_ICONQUESTION = 0x20;
    const uint MB_ICONINFORMATION = 0x40;
    const uint MB_SYSTEMMODAL = 0x1000;     // float above other windows when we have no owner window
    const uint MB_SETFOREGROUND = 0x10000;

    public enum Result { Ok = 1, Cancel = 2, Yes = 6, No = 7 }

    static string Caption => AppConstants.AppTitle;

    /// <summary>Shows the box on a worker thread so it never deadlocks the UI message loop, and awaits it.</summary>
    static Task<Result> ShowAsync(string text, string caption, uint type) =>
        Task.Run(() => (Result)MessageBoxW(IntPtr.Zero, text, caption, type | MB_SYSTEMMODAL | MB_SETFOREGROUND));

    public static void Error(string message) =>
        MessageBoxW(IntPtr.Zero, message, Caption, MB_OK | MB_ICONERROR | MB_SYSTEMMODAL | MB_SETFOREGROUND);

    public static void Info(string message) =>
        MessageBoxW(IntPtr.Zero, message, Caption, MB_OK | MB_ICONINFORMATION | MB_SYSTEMMODAL | MB_SETFOREGROUND);

    public static void Warn(string message) =>
        MessageBoxW(IntPtr.Zero, message, Caption, MB_OK | MB_ICONWARNING | MB_SYSTEMMODAL | MB_SETFOREGROUND);

    /// <summary>Yes/No question. Returns true for Yes.</summary>
    public static bool Confirm(string message) =>
        MessageBoxW(IntPtr.Zero, message, Caption, MB_YESNO | MB_ICONQUESTION | MB_SYSTEMMODAL | MB_SETFOREGROUND) == (int)Result.Yes;

    /// <summary>Yes/No question shown off the UI thread (awaitable). Returns true for Yes.</summary>
    public static async Task<bool> ConfirmAsync(string message) =>
        await ShowAsync(message, Caption, MB_YESNO | MB_ICONQUESTION) == Result.Yes;

    /// <summary>Error dialog shown off the UI thread (awaitable).</summary>
    public static Task ErrorAsync(string message) => ShowAsync(message, Caption, MB_OK | MB_ICONERROR);
}
