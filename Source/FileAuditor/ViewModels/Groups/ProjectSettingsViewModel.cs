namespace FileAuditor.ViewModels.Groups;

/// <summary>The Project Settings group: the save-with-the-report tick and a message saying where the project's settings go (SDD §5).</summary>
public sealed partial class ProjectSettingsViewModel : ObservableObject
{
    private const string DifferentFolderMessage = "Saved to the opened settings file, in a different folder from the audit reports.";

    private const string NotSavedMessage = "This project's settings are not being saved.";

    private const string SameFolderMessage = "Saved as FileAuditorSettings.json in the same folder as the audit reports.";

    private readonly ProjectSettingsService _project;

    /// <summary>Initialises a new instance with its own default state, for the XAML designer.</summary>
    public ProjectSettingsViewModel()
        : this(new ReportLocationState(), new ProjectSettingsService(new SettingsStore(), new DialogueService(() => null)))
    { }

    /// <summary>Initialises a new instance over the shared report location and the open project.</summary>
    /// <param name="shared">The location values and the save tick that every group shares.</param>
    /// <param name="project">Holds the open project's settings file.</param>
    public ProjectSettingsViewModel(ReportLocationState shared, ProjectSettingsService project)
    {
        Shared   = shared;
        _project = project;

        Shared.PropertyChanged   += OnSharedChanged;
        _project.FilePathChanged += OnFilePathChanged;

        Message = ComposeMessage();
    }

    /// <summary>Gets the message saying where, or whether, the project's settings are saved.</summary>
    [ ObservableProperty ]
    public partial string Message { get; private set; }

    /// <summary>Gets the location values and the save tick shared with the other groups.</summary>
    public ReportLocationState Shared { get; }

    private void OnSharedChanged(object? sender, PropertyChangedEventArgs e) => Message = ComposeMessage();

    private void OnFilePathChanged(object? sender, EventArgs e) => Message = ComposeMessage();

    private string ComposeMessage()
    {
        if (!Shared.SaveProjectSettings)
        {
            return NotSavedMessage;
        }

        if (_project.FilePath is null)
        {
            return SameFolderMessage;
        }

        ReportPath folder = Shared.ResolveFolder();

        return folder.IsComplete && _project.IsInFolder(folder.Directory) ? SameFolderMessage : DifferentFolderMessage;
    }
}
