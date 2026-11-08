namespace FileAuditor.ViewModels.Groups;

/// <summary>The Audit group and status bar: read-only run statistics as display strings, blank before the first run.</summary>
public sealed partial class AuditViewModel : AuditStatusBase
{
    private readonly RemainingEstimator _remainingEstimator = new();

    /// <summary>Gets or sets the number of files checked so far, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Checked { get; set; } = "";

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Elapsed { get; set; } = "";

    /// <summary>Gets or sets the files checked per second over the whole run, as a whole number; blank until a full second has elapsed.</summary>
    [ ObservableProperty ]
    public partial string FilesPerSecond { get; set; } = "";

    /// <summary>Gets or sets the number of missing files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Missing { get; set; } = "";

    /// <summary>Gets or sets the progress-bar position, from 0 to 100.</summary>
    [ ObservableProperty ]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the estimated remaining time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Remaining { get; set; } = "";

    /// <summary>Gets or sets the number of skipped rows, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Skipped { get; set; } = "";

    /// <summary>Gets or sets the total number of files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string TotalFiles { get; set; } = "";

    /// <summary>Gets or sets the number of unverified files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unverified { get; set; } = "";

    /// <inheritdoc />
    public override void Reset()
    {
        _remainingEstimator.Reset();

        TotalFiles     = "";
        Checked        = "";
        Missing        = "";
        Unverified     = "";
        Skipped        = "";
        Elapsed        = "";
        Remaining      = "";
        FilesPerSecond = "";
        Percent        = 0;
        StatusPercent  = "";
    }

    /// <summary>Refreshes the statistics from a running audit.</summary>
    /// <param name="progress">The audit's live counters.</param>
    /// <param name="elapsed">The time since the run started.</param>
    /// <param name="isFinished">Whether the run has ended, which makes an empty table read 100%.</param>
    public void Update(AuditProgress progress, TimeSpan elapsed, bool isFinished)
    {
        long   total   = Math.Max(progress.TotalFiles, progress.Processed);
        long   done    = progress.Checked;
        long   handled = progress.Processed;

        // The displayed rate is over the whole run, the span the Elapsed box shows; Remaining uses the recent checking-time rate, which excludes the counting phase.
        double wholeRunSeconds = elapsed.TotalSeconds;

        TotalFiles     = progress.HasTotal ? total.ToString(format: "N0", CultureInfo.CurrentCulture) : "";
        Checked        = done.ToString(format: "N0", CultureInfo.CurrentCulture);
        Missing        = progress.Missing.ToString(format: "N0", CultureInfo.CurrentCulture);
        Unverified     = progress.Unverified.ToString(format: "N0", CultureInfo.CurrentCulture);
        Skipped        = progress.Skipped.ToString(format: "N0", CultureInfo.CurrentCulture);
        Elapsed        = RemainingEstimator.FormatTime(elapsed.TotalSeconds);
        Remaining      = _remainingEstimator.Next(handled, total, progress.CheckingElapsed, progress.HasTotal, isFinished);
        FilesPerSecond = wholeRunSeconds >= 1 ? (done / wholeRunSeconds).ToString(format: "N0", CultureInfo.CurrentCulture) : "";

        Percent       = total > 0 ? Math.Min(val1: 100, handled * 100.0 / total) : isFinished && progress.HasTotal ? 100 : 0;
        StatusPercent = progress.HasTotal ? $"{Percent:0}%" : "";
    }
}
