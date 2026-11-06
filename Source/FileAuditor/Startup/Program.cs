namespace FileAuditor.Startup;

/// <summary>Application entry point.</summary>
internal static class Program
{
    // Initialisation code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialised
    // yet and stuff might break.

    /// <summary>Starts the application with the classic desktop lifetime.</summary>
    /// <param name="args">The command-line arguments.</param>
    [ STAThread ]
    public static void Main(string[] args)
        => BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.

    /// <summary>Builds the Avalonia application; also used by the visual designer.</summary>
    /// <returns>The configured <see cref="AppBuilder" />.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
                     .UsePlatformDetect()
                     .LogToTrace();
}
