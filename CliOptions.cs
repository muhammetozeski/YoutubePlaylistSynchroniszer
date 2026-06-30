namespace YoutubePlaylistSynchroniszer;

/// <summary>How the process was asked to run.</summary>
internal enum LaunchMode
{
    /// <summary>Normal double-click: show the main window.</summary>
    Gui,
    /// <summary>Headless background sync: tray icon only, run saved profiles, then exit.</summary>
    Sync,
    /// <summary>Print help/version to the console and exit.</summary>
    Cli,
}

/// <summary>A command-line flag with a long and short alias (the VirusTotalScanner CmdArg pattern).</summary>
internal sealed class CmdArg(string longName, string shortName)
{
    public bool IsMatch(string input) =>
        !string.IsNullOrWhiteSpace(input) &&
        (input.Equals(longName, StringComparison.OrdinalIgnoreCase) ||
         input.Equals(shortName, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Parsed command-line options and the resolved <see cref="LaunchMode"/>.</summary>
internal sealed class CliOptions
{
    public bool Sync;
    public bool ForceGui;
    public bool ShowHelp;
    public bool ShowVersion;

    public LaunchMode Mode =>
        ShowHelp || ShowVersion ? LaunchMode.Cli :
        Sync ? LaunchMode.Sync :
        LaunchMode.Gui;

    static readonly CmdArg SyncArg = new("--sync", "--background");
    static readonly CmdArg GuiArg = new("--gui", "-g");
    static readonly CmdArg HelpArg = new("--help", "-h");
    static readonly CmdArg VersionArg = new("--version", "-v");

    public static CliOptions Parse(string[] args)
    {
        var options = new CliOptions();
        foreach (var arg in args)
        {
            if (SyncArg.IsMatch(arg)) options.Sync = true;
            else if (GuiArg.IsMatch(arg)) options.ForceGui = true;
            else if (HelpArg.IsMatch(arg) || arg is "-?" or "/?") options.ShowHelp = true;
            else if (VersionArg.IsMatch(arg)) options.ShowVersion = true;
        }
        if (options.ForceGui) options.Sync = false;
        return options;
    }
}
