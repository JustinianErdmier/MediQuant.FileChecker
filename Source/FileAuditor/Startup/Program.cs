namespace FileAuditor.Startup;

/// <summary>Application entry point.</summary>
internal static class Program
{
    /// <summary>Starts the application with the classic desktop lifetime.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <remarks>
    ///     Initialisation code. Don't use any Avalonia, third-party APIs or any SynchronizationContext-reliant code before AppMain is called: things aren't initialised yet and stuff
    ///     might break.
    /// </remarks>
    [ STAThread ]
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception exception)
        {
            UnexpectedErrorHandler.ReportFatalAndExit(exception);
        }
    }

    /// <summary>Builds the Avalonia application; also used by the visual designer.</summary>
    /// <returns>The configured <see cref="AppBuilder" />.</returns>
    /// <remarks>Avalonia configuration, don't remove; also used by the visual designer.</remarks>
    private static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .LogToTrace();
}
