namespace FileAuditor.ViewModels.Groups;

/// <summary>The Report group: the project folder, the two stage choices and the report location, either built from them or typed in (SDD §5.3).</summary>
public sealed partial class ReportViewModel : ObservableObject
{
    private string _customFileName = "FileAuditorReport.txt";

    private string _customLocation = "";

    private bool _isRefreshing;

    /// <summary>Initialises a new instance with the default settings, for the XAML designer.</summary>
    public ReportViewModel()
        : this(AppSettings.Default)
    { }

    /// <summary>Initialises a new instance populated from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public ReportViewModel(AppSettings settings)
    {
        Current = ReportPathResolver.Resolve(settings);
        Load(settings);
    }

    /// <summary>Gets the report path the current inputs resolve to.</summary>
    public ReportPath Current { get; private set; }

    /// <summary>Gets or sets the text of the File name box: a read-only preview when built, the typed name when custom.</summary>
    [ ObservableProperty ]
    public partial string FileName { get; set; } = "";

    /// <summary>Gets a value indicating whether the Audit Stage group is editable: built, and not forced to Interim by the Sample stage.</summary>
    public bool IsAuditStageEnabled => IsBuilt && !IsSampleProject;

    /// <summary>Gets a value indicating whether the report location is built, so the project folder and stage groups apply.</summary>
    public bool IsBuilt => !UseCustomLocation;

    /// <summary>Gets or sets a value indicating whether the Final audit stage is selected.</summary>
    [ ObservableProperty ]
    public partial bool IsFinalAudit { get; set; }

    /// <summary>Gets or sets a value indicating whether the Final project stage is selected.</summary>
    [ ObservableProperty ]
    public partial bool IsFinalProject { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the Interim audit stage is selected.</summary>
    [ ObservableProperty ]
    public partial bool IsInterimAudit { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the Sample project stage is selected.</summary>
    [ ObservableProperty ]
    public partial bool IsSampleProject { get; set; }

    /// <summary>Gets or sets the text of the Location box: a read-only preview when built, the typed folder when custom.</summary>
    [ ObservableProperty ]
    public partial string Location { get; set; } = "";

    /// <summary>Gets or sets the project folder used to build the report location.</summary>
    [ ObservableProperty ]
    public partial string ProjectFolder { get; set; } = "";

    /// <summary>
    ///     Gets or sets a value indicating whether the project's settings are saved automatically with the report. It is a per-session choice and is deliberately not part of
    ///     <see cref="ApplyTo" /> or <see cref="Load" />.
    /// </summary>
    [ ObservableProperty ]
    public partial bool SaveProjectSettings { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the report goes to a custom location and file name.</summary>
    [ ObservableProperty ]
    public partial bool UseCustomLocation { get; set; }

    /// <summary>Copies this group's inputs into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this group's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => settings with
        {
            ProjectFolder = ProjectFolder,
            ProjectStage = IsSampleProject ? ProjectStage.Sample : ProjectStage.Final,
            AuditStage = IsFinalAudit ? AuditStage.Final : AuditStage.Interim,
            UseCustomReportLocation = UseCustomLocation,
            CustomLocation = _customLocation,
            CustomFileName = _customFileName
        };

    /// <summary>Sets this group's inputs from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        _customLocation = settings.CustomLocation;
        _customFileName = settings.CustomFileName;

        ProjectFolder     = settings.ProjectFolder;
        IsSampleProject   = settings.ProjectStage == ProjectStage.Sample;
        IsFinalProject    = !IsSampleProject;
        IsInterimAudit    = IsSampleProject || settings.AuditStage == AuditStage.Interim;
        IsFinalAudit      = !IsInterimAudit;
        UseCustomLocation = settings.UseCustomReportLocation;

        Refresh();
    }

    partial void OnProjectFolderChanged(string value) => Refresh();

    partial void OnIsSampleProjectChanged(bool value)
    {
        if (value)
        {
            IsInterimAudit = true;
            IsFinalAudit   = false;
        }

        OnPropertyChanged(nameof(IsAuditStageEnabled));
        Refresh();
    }

    partial void OnIsFinalProjectChanged(bool value) => Refresh();

    partial void OnIsInterimAuditChanged(bool value) => Refresh();

    partial void OnIsFinalAuditChanged(bool value) => Refresh();

    partial void OnUseCustomLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBuilt));
        OnPropertyChanged(nameof(IsAuditStageEnabled));
        Refresh();
    }

    partial void OnLocationChanged(string value)
    {
        if (_isRefreshing || !UseCustomLocation)
        {
            return;
        }

        _customLocation = value;
        UpdateCurrent();
    }

    partial void OnFileNameChanged(string value)
    {
        if (_isRefreshing || !UseCustomLocation)
        {
            return;
        }

        _customFileName = value;
        UpdateCurrent();
    }

    /// <summary>Rewrites the Location and File name boxes for the current mode and recomputes <see cref="Current" />.</summary>
    private void Refresh()
    {
        ReportPath path = ReportPathResolver.Resolve(ApplyTo(AppSettings.Default));

        _isRefreshing = true;
        Location      = UseCustomLocation ? _customLocation : path.DisplayDirectory;
        FileName      = UseCustomLocation ? _customFileName : path.FileName;
        _isRefreshing = false;

        UpdateCurrent();
    }

    private void UpdateCurrent()
    {
        Current = ReportPathResolver.Resolve(ApplyTo(AppSettings.Default));
        OnPropertyChanged(nameof(Current));
    }
}
