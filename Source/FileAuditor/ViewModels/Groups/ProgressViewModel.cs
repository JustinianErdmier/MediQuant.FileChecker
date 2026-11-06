namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     The Progress group and status bar: read-only run statistics as display strings, blank before the first run.
/// </summary>
public sealed partial class ProgressViewModel : ObservableObject
{
    /// <summary>Gets or sets the total number of files, formatted with thousands separators.</summary>
    [ObservableProperty]
    public partial string TotalFiles { get; set; } = "";

    /// <summary>Gets or sets the number of files checked so far, formatted with thousands separators.</summary>
    [ObservableProperty]
    public partial string Checked { get; set; } = "";

    /// <summary>Gets or sets the number of missing files, formatted with thousands separators.</summary>
    [ObservableProperty]
    public partial string Missing { get; set; } = "";

    /// <summary>Gets or sets the number of unverified files, formatted with thousands separators.</summary>
    [ObservableProperty]
    public partial string Unverified { get; set; } = "";

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ObservableProperty]
    public partial string Elapsed { get; set; } = "";

    /// <summary>Gets or sets the estimated remaining time as <c>hh:mm:ss</c>.</summary>
    [ObservableProperty]
    public partial string Remaining { get; set; } = "";

    /// <summary>Gets or sets the checking rate, to two decimal places.</summary>
    [ObservableProperty]
    public partial string FilesPerSecond { get; set; } = "";

    /// <summary>Gets or sets the progress-bar position, from 0 to 100.</summary>
    [ObservableProperty]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the status-bar message.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Ready";

    /// <summary>Gets or sets the status-bar percentage text, blank when no run is under way.</summary>
    [ObservableProperty]
    public partial string StatusPercent { get; set; } = "";
}
