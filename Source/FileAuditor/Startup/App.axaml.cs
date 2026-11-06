using FileAuditor.ViewModels.Main;
using FileAuditor.Views.Main;

namespace FileAuditor.Startup;

/// <summary>The File Auditor application: loads the Classic theme and the saved settings, and shows the main window.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SettingsStore store = new();

            // An unreadable settings file is treated as "use the defaults".
            ErrorOr<AppSettings> loaded = store.Load();
            AppSettings settings = loaded.IsError ? AppSettings.Default : loaded.Value;
            MainWindowViewModel viewModel = new(settings, new DialogueService(() => desktop.MainWindow), store);

            desktop.MainWindow = new MainWindow { DataContext = viewModel };

            // A failed save is deliberately ignored: the user loses nothing but the convenience of remembered inputs.
            desktop.Exit += (_, _) => store.Save(viewModel.Snapshot());
        }

        base.OnFrameworkInitializationCompleted();
    }
}
