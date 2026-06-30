using System.Runtime.InteropServices;

namespace YoutubePlaylistSynchroniszer;

/// <summary>
/// A WinExe has no console of its own. When launched from a terminal we attach to the parent console so
/// <c>--help</c> / <c>--version</c> output is visible; otherwise these calls are harmless no-ops.
/// </summary>
internal static partial class ConsoleBootstrap
{
    const int AttachParentProcess = -1;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    public static bool TryAttachParentConsole()
    {
        try { return AttachConsole(AttachParentProcess); }
        catch { return false; }
    }
}
