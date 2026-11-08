namespace FileAuditor.ViewModels.Groups;

/// <summary>What both Audit groups share: the status-bar text and percentage, which follow the running audit whichever tab is selected. It has no view of its own.</summary>
public abstract partial class AuditStatusBase : ObservableObject
{
    /// <summary>Gets or sets the status-bar message.</summary>
    [ ObservableProperty ]
    public partial string StatusMessage { get; set; } = "";

    /// <summary>Gets or sets the status-bar percentage text, blank when no run is under way or the percentage is not known.</summary>
    [ ObservableProperty ]
    public partial string StatusPercent { get; set; } = "";

    /// <summary>Gets or sets whether this tab's Stop button is enabled: true only while this tab's audit is the one running.</summary>
    [ ObservableProperty ]
    public partial bool IsStopEnabled { get; set; }

    /// <summary>Blanks every statistic and the progress bar, ready for a new run.</summary>
    public abstract void Reset();
}
