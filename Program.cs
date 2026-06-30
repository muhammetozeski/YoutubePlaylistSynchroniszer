namespace YoutubePlaylistSynchroniszer;

internal static class Program
{
    /// <summary>
    /// Hybrid entry point. One WinExe behaves as:
    ///   • double-click / no args -> full GUI
    ///   • <c>--sync</c>          -> headless background sync (tray icon only), then exit
    ///   • <c>--help/--version</c> from a terminal -> CLI text, no GUI
    /// The whole run sits inside <see cref="Resilience.GuardAsync"/> so the only place an error is
    /// swallowed is the outermost guard, which asks the user whether to retry.
    /// </summary>
    [STAThread]
    static int Main(string[] args)
    {
        var options = CliOptions.Parse(args);
        InitCore();

        return options.Mode switch
        {
            LaunchMode.Cli => RunCli(options),
            LaunchMode.Sync => RunSync(),
            _ => RunGui(),
        };
    }

    static void InitCore()
    {
        SettingsManager.LoadSettings();
        LoggerHost.Initialize();
        LocManager.Init();
        CredentialStore.Load();
        InstallGlobalExceptionLogging();
        Log($"{AppConstants.AppTitle} v{AppConstants.Version} starting (data: {ConfigPathResolver.DataFolder})", LogLevel.Info);
    }

    /// <summary>
    /// Safety net: nothing crashes silently. Every unhandled exception (background threads, unobserved
    /// tasks) is forced to Error logging. Hardening means logging, not hiding.
    /// </summary>
    static void InstallGlobalExceptionLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log("UNHANDLED exception: " + ((e.ExceptionObject as Exception)?.ToString() ?? e.ExceptionObject?.ToString()), LogLevel.Error);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log("UNOBSERVED task exception: " + e.Exception, LogLevel.Error);
            e.SetObserved();
        };
    }

    static int RunCli(CliOptions options)
    {
        ConsoleBootstrap.TryAttachParentConsole();
        if (options.ShowVersion) Console.WriteLine($"{AppConstants.AppTitle} v{AppConstants.Version}");
        else Console.WriteLine(string.Format(Strings.HelpTextFormat, AppConstants.AppTitle, AppConstants.Version));
        return 0;
    }

    static int RunGui()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            Log("UNHANDLED UI exception: " + e.Exception, LogLevel.Error);
            try { NativeMessageBox.Error(string.Format(Strings.UnexpectedErrorFormat, e.Exception.Message)); }
            catch (Exception ex) { Log("Error dialog failed: " + ex.Message, LogLevel.Warning); }
        };

        Application.Run(new MainForm());
        return 0;
    }

    static int RunSync()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new SyncTrayContext());
        return 0;
    }
}
