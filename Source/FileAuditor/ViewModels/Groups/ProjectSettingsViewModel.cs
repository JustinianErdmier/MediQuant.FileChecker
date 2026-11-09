namespace FileAuditor.ViewModels.Groups;

/// <summary>The Project Settings group: the save-with-the-report tick and a message saying where the project's settings go.</summary>
public sealed partial class ProjectSettingsViewModel : ObservableObject
{
    /// <summary>The message for a project whose settings file is open in a different folder from the audit reports.</summary>
    private const string DifferentFolderMessage = "Saved to the opened settings file, in a different folder from the audit reports.";

    /// <summary>The message for a project whose settings are not saved.</summary>
    private const string NotSavedMessage = "This project's settings are not being saved.";

    /// <summary>The message for a project whose settings are saved beside the audit reports.</summary>
    private const string SameFolderMessage = "Saved as FileAuditorSettings.json in the same folder as the audit reports.";

    /// <summary>Holds the open project's settings file.</summary>
    private readonly ProjectSettingsService _projectSettingsService;

    /// <summary>Initialises a new instance with its own default state, for the XAML designer.</summary>
    public ProjectSettingsViewModel()
        : this(new ReportLocationState(), new ProjectSettingsService(new DialogueService(() => null)))
    { }

    /// <summary>Initialises a new instance over the shared report location and the open project.</summary>
    /// <param name="shared">The location values and the save tick that every group shares.</param>
    /// <param name="projectSettingsService">Holds the open project's settings file.</param>
    public ProjectSettingsViewModel(ReportLocationState shared, ProjectSettingsService projectSettingsService)
    {
        Shared                  = shared;
        _projectSettingsService = projectSettingsService;

        Shared.PropertyChanged                  += OnSharedChanged;
        _projectSettingsService.FilePathChanged += OnFilePathChanged;

        Message = ComposeMessage();
    }

    /// <summary>Gets the message saying where, or whether, the project's settings are saved.</summary>
    [ ObservableProperty ]
    public partial string Message { get; private set; }

    /// <summary>Gets the location values and the save tick shared with the other groups.</summary>
    public ReportLocationState Shared { get; }

    /// <summary>Refreshes the message when a shared location value or the save tick changes.</summary>
    /// <param name="sender">The shared state that changed.</param>
    /// <param name="eventArgs">The name of the property that changed.</param>
    private void OnSharedChanged(object? sender, PropertyChangedEventArgs eventArgs) => Message = ComposeMessage();

    /// <summary>Refreshes the message when the project's settings file is opened, reset or first saved.</summary>
    /// <param name="sender">The project settings service.</param>
    /// <param name="eventArgs">The event data, which is empty.</param>
    private void OnFilePathChanged(object? sender, EventArgs eventArgs) => Message = ComposeMessage();

    /// <summary>Works out which message applies: not saved, saved beside the reports, or saved in a different folder.</summary>
    /// <returns>The message to show.</returns>
    private string ComposeMessage()
    {
        if (!Shared.SaveProjectSettings)
        {
            return NotSavedMessage;
        }

        if (_projectSettingsService.FilePath is null)
        {
            return SameFolderMessage;
        }

        ReportPath folder = Shared.ResolveFolder();

        return folder.IsComplete && _projectSettingsService.IsInFolder(folder.Directory) ? SameFolderMessage : DifferentFolderMessage;
    }
}
