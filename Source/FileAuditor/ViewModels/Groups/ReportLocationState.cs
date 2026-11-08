namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     The Report group's values that every audit direction shares: the project folder, the two stage choices, the custom-location flag and text, and the save-settings tick (SDD
///     §5.3). One instance is owned by the main window and every direction's <see cref="ReportViewModel" /> reads it, so the tabs cannot drift apart.
/// </summary>
public sealed partial class ReportLocationState : ObservableObject
{
    /// <summary>Initialises a new instance with the default settings.</summary>
    public ReportLocationState() => Load(AppSettings.Default);

    /// <summary>Gets or sets the typed report folder, kept even while the built location is shown.</summary>
    [ ObservableProperty ]
    public partial string CustomLocation { get; set; } = string.Empty;

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

    /// <summary>Gets or sets the project folder used to build the report location.</summary>
    [ ObservableProperty ]
    public partial string ProjectFolder { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets a value indicating whether the project's settings are saved automatically with the report. It is a per-session choice and is deliberately not part of
    ///     <see cref="ApplyTo" /> or <see cref="Load" />.
    /// </summary>
    [ ObservableProperty ]
    public partial bool SaveProjectSettings { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the report goes to a custom location and file name.</summary>
    [ ObservableProperty ]
    public partial bool UseCustomLocation { get; set; }

    /// <summary>Copies the shared values into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying the shared values.</returns>
    public AppSettings ApplyTo(AppSettings settings)
        => settings with
        {
            ProjectFolder = ProjectFolder,
            ProjectStage = IsSampleProject ? ProjectStages.Sample : ProjectStages.Final,
            AuditStage = IsFinalAudit ? AuditStages.Final : AuditStages.Interim,
            UseCustomReportLocation = UseCustomLocation,
            CustomLocation = CustomLocation
        };

    /// <summary>
    ///     Resolves the report folder from the shared values alone, which is what decides where the project's settings file goes (SDD §8.3). The result has no file name, so it is
    ///     the same whichever tab is selected.
    /// </summary>
    /// <returns>The report folder: complete once the project folder is filled in (built) or the custom location is (custom).</returns>
    public ReportPath ResolveFolder() => ReportPathResolver.ResolveFolder(ApplyTo(AppSettings.Default));

    /// <summary>Sets the shared values from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        CustomLocation = settings.CustomLocation;

        ProjectFolder     = settings.ProjectFolder;
        IsSampleProject   = settings.ProjectStage is ProjectStages.Sample;
        IsFinalProject    = !IsSampleProject;
        IsInterimAudit    = IsSampleProject || settings.AuditStage is AuditStages.Interim;
        IsFinalAudit      = !IsInterimAudit;
        UseCustomLocation = settings.UseCustomReportLocation;
    }

    partial void OnIsSampleProjectChanged(bool value)
    {
        if (value)
        {
            IsInterimAudit = true;
            IsFinalAudit   = false;
        }

        OnPropertyChanged(nameof(IsAuditStageEnabled));
    }

    partial void OnUseCustomLocationChanged(bool value)
    {
        OnPropertyChanged(nameof(IsBuilt));
        OnPropertyChanged(nameof(IsAuditStageEnabled));
    }
}
