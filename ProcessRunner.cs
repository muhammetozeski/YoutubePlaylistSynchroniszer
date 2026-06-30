using System.Diagnostics;
using System.Text;

namespace YoutubePlaylistSynchroniszer;

/// <summary>The outcome of a finished external process.</summary>
internal sealed record ProcessResult(int ExitCode, string StandardError);

/// <summary>
/// Runs an external console tool (yt-dlp / ffmpeg) with arguments passed via <see cref="ProcessStartInfo.ArgumentList"/>
/// (so quoting is never hand-rolled), streaming each stdout/stderr line to a callback and killing the whole
/// process tree if the cancellation token fires.
/// </summary>
internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string exePath, IReadOnlyList<string> arguments,
        string? workingDirectory = null, Action<string>? onOutputLine = null, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(exePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory,
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var errorBuffer = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onOutputLine?.Invoke(e.Data); };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null) return;
            onOutputLine?.Invoke(e.Data);
            lock (errorBuffer) errorBuffer.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Could not start process: {exePath}");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using var cancelKill = cancellationToken.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (Exception ex) { Log("Process kill failed: " + ex.Message, LogLevel.Warning); }
        });

        await process.WaitForExitAsync(cancellationToken);
        string capturedErrors;
        lock (errorBuffer) capturedErrors = errorBuffer.ToString();
        return new ProcessResult(process.ExitCode, capturedErrors);
    }
}
