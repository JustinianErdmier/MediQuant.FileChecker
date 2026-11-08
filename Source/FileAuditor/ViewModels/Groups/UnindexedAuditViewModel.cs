namespace FileAuditor.ViewModels.Groups;

/// <summary>The Audit group of the Unindexed Files tab: read-only run statistics as display strings, blank before the first run. It has no Remaining time.</summary>
public sealed partial class UnindexedAuditViewModel : AuditStatusBase
{
    // How far the sweep advances on each refresh; with refreshes four times a second the bar crosses in about three seconds.
    private const double SweepStep = 9;

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Elapsed { get; set; } = "";

    /// <summary>Gets or sets the files found per second over the whole run, to two decimal places; blank until a full second has elapsed.</summary>
    [ ObservableProperty ]
    public partial string FilesPerSecond { get; set; } = "";

    /// <summary>Gets or sets the number of files found that are in the index, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Indexed { get; set; } = "";

    /// <summary>
    ///     Gets or sets the progress-bar position, from 0 to 100. The total is unknown, so while a run is under way the bar sweeps from empty to full and starts again, and it is
    ///     full when the run ends. (The Classic theme draws nothing for <c>ProgressBar.IsIndeterminate</c>, so the sweep is driven from here.)
    /// </summary>
    [ ObservableProperty ]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the number of files found so far, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string TotalFiles { get; set; } = "";

    /// <summary>Gets or sets the number of files found that are not in the index, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unindexed { get; set; } = "";

    /// <summary>Gets or sets the number of folders that could not be listed, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unverified { get; set; } = "";

    /// <inheritdoc />
    public override void Reset()
    {
        TotalFiles      = "";
        Indexed         = "";
        Unindexed       = "";
        Unverified      = "";
        Elapsed         = "";
        FilesPerSecond  = "";
        Percent         = 0;
        StatusPercent   = "";
    }

    /// <summary>Refreshes the statistics from a running audit.</summary>
    /// <param name="progress">The audit's live counters.</param>
    /// <param name="elapsed">The time since the run started.</param>
    /// <param name="isFinished">Whether the run has ended, which fills the progress bar; otherwise each refresh advances its sweep.</param>
    public void Update(UnindexedProgress progress, TimeSpan elapsed, bool isFinished)
    {
        long total = progress.TotalFiles;

        TotalFiles     = total.ToString(format: "N0", CultureInfo.CurrentCulture);
        Indexed        = progress.Indexed.ToString(format: "N0", CultureInfo.CurrentCulture);
        Unindexed      = progress.Unindexed.ToString(format: "N0", CultureInfo.CurrentCulture);
        Unverified     = progress.Unverified.ToString(format: "N0", CultureInfo.CurrentCulture);
        Elapsed        = $"{(long)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
        FilesPerSecond = elapsed.TotalSeconds >= 1 ? (total / elapsed.TotalSeconds).ToString(format: "0.00", CultureInfo.CurrentCulture) : "";

        Percent       = isFinished ? 100 : Percent >= 100 ? 0 : Math.Min(val1: 100, Percent + SweepStep);
        StatusPercent = isFinished ? "100%" : "";
    }
}
