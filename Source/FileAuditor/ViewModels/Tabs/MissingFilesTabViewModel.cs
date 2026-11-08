namespace FileAuditor.ViewModels.Tabs;

/// <summary>The Missing Files tab: its Options group, its direction's Report group and its Audit group.</summary>
public sealed partial class MissingFilesTabViewModel : ObservableObject
{
    /// <summary>Initialises a new instance with its own default shared state, for the XAML designer.</summary>
    public MissingFilesTabViewModel()
        : this(new ReportLocationState())
    { }

    /// <summary>Initialises a new instance over the shared report location.</summary>
    /// <param name="shared">The report location values every tab shares.</param>
    public MissingFilesTabViewModel(ReportLocationState shared)
    {
        Report = new ReportViewModel(shared, AuditDirection.Missing);
        Audit  = new AuditViewModel();
    }

    /// <summary>Gets the Audit group.</summary>
    public AuditViewModel Audit { get; }

    /// <summary>Gets the Report group for the missing-files direction.</summary>
    public ReportViewModel Report { get; }

    /// <summary>Gets or sets a value indicating whether zero-byte files are reported as missing.</summary>
    [ ObservableProperty ]
    public partial bool ReportEmptyFiles { get; set; } = true;

    /// <summary>Copies this tab's inputs into the given settings.</summary>
    /// <param name="settings">The settings to start from.</param>
    /// <returns>A copy of <paramref name="settings" /> carrying this tab's inputs.</returns>
    public AppSettings ApplyTo(AppSettings settings) => Report.ApplyTo(settings with { ReportEmptyFiles = ReportEmptyFiles });

    /// <summary>Sets this tab's inputs from the given settings.</summary>
    /// <param name="settings">The settings to load.</param>
    public void Load(AppSettings settings)
    {
        ReportEmptyFiles = settings.ReportEmptyFiles;

        Report.Load(settings);
    }
}
