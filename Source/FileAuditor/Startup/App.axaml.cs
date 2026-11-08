namespace FileAuditor.Startup;

/// <summary>The File Auditor application: loads the Classic theme and shows the main window, starting from the default settings.</summary>
public class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            UnexpectedErrorHandler.Install(desktop);

            DialogueService      dialogues = new(() => desktop.MainWindow);
            ProjectSettingsService project   = new(dialogues);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(dialogues, project)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
