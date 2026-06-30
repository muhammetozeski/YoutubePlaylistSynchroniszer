using System.Diagnostics;
using System.Text.RegularExpressions;

// Adapted from C:\E\KodlamaProjeleri\CSharp\VirusTotalScanner\HelperFunctions.cs.
// Lives in the GLOBAL namespace so "global using static HelperFunctions;" exposes these
// everywhere unqualified.
#pragma warning disable CA1050 // Declare types in namespaces
internal static partial class HelperFunctions
#pragma warning restore CA1050
{
    /// <summary>Inserts spaces before capitals/numbers: "AutoCopyToClipboard" -> "Auto Copy To Clipboard".</summary>
    public static string SplitCamelCase(string input) => SplitCamelCaseRegex().Replace(input, " $1").Trim();

    /// <summary>Human-readable byte size.</summary>
    public static string FormatBytes(long bytes)
    {
        const long KB = 1024, MB = KB * 1024, GB = MB * 1024, TB = GB * 1024;
        if (bytes < KB) return $"{bytes} B";
        if (bytes < MB) return $"{(double)bytes / KB:F2} KB";
        if (bytes < GB) return $"{(double)bytes / MB:F2} MB";
        if (bytes < TB) return $"{(double)bytes / GB:F2} GB";
        return $"{(double)bytes / TB:F2} TB";
    }

    /// <summary>Opens a URL in the default browser.</summary>
    public static void OpenUrlInBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Log("Could not open browser: " + ex.Message, LogLevel.Warning); }
    }

    /// <summary>Opens a file/folder with the default program / Explorer.</summary>
    public static void OpenWithDefaultProgram(string path)
    {
        try
        {
            using var p = new Process();
            p.StartInfo.FileName = "explorer";
            p.StartInfo.Arguments = "\"" + path + "\"";
            p.Start();
        }
        catch (Exception ex) { Log("Could not open path: " + ex.Message, LogLevel.Warning); }
    }

    /// <summary>Selects a file in Explorer.</summary>
    public static void RevealInExplorer(string path)
    {
        try { Process.Start("explorer.exe", $"/select,\"{path}\""); }
        catch (Exception ex) { Log("Could not reveal in Explorer: " + ex.Message, LogLevel.Warning); }
    }

    /// <summary>Shortens a long path with an ellipsis in the middle for display.</summary>
    public static string TruncateMiddle(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
        int keep = (max - 1) / 2;
        return text[..keep] + "…" + text[^keep..];
    }

    /// <summary>Replaces characters Windows forbids in file names with an underscore (yt-dlp does its own
    /// sanitizing on download; this is for our own display/derived paths).</summary>
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name)) return "_";
        foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Trim();
    }

    /// <summary>Pulls the 11-char YouTube id out of a "Title (VIDEOID).ext" file name, or null if the
    /// trailing parenthesised token is not a valid id. Mirrors the regex used by the reference ps1.</summary>
    public static string? ExtractVideoId(string fileNameWithoutExtension)
    {
        if (string.IsNullOrEmpty(fileNameWithoutExtension)) return null;
        var match = TrailingParenIdRegex().Match(fileNameWithoutExtension);
        if (!match.Success) return null;
        string candidate = match.Groups[1].Value;
        return VideoIdRegex().IsMatch(candidate) ? candidate : null;
    }

    public static void DeleteOldestFiles(string folderPath, int filesToKeep, string prefix = "")
    {
        if (!Directory.Exists(folderPath)) return;

        var datedFiles = new List<(string Path, DateTime Date)>();
        foreach (var p in Directory.GetFiles(folderPath))
        {
            string name = Path.GetFileNameWithoutExtension(p);
            name = name.Contains(prefix) ? name.Remove(name.IndexOf(prefix), prefix.Length) : name;
            name = name.Trim();
            if (DateTime.TryParseExact(name, "yyyy.MM.dd HH.mm.ss.ff",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out DateTime d))
                datedFiles.Add((p, d));
        }

        var sorted = datedFiles.OrderBy(f => f.Date).ToList();
        for (int i = 0; i < sorted.Count - filesToKeep; i++)
        {
            try { File.Delete(sorted[i].Path); } catch { }
        }
    }

    [GeneratedRegex("([A-Z0-9])")]
    private static partial Regex SplitCamelCaseRegex();

    [GeneratedRegex(@".*\(([^()]+)\)\s*$")]
    private static partial Regex TrailingParenIdRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)]
    private static partial Regex VideoIdRegex();
}
