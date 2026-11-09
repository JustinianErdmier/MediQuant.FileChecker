namespace FileAuditor.ViewModels.Groups;

/// <summary>The Audit group and status bar: read-only run statistics as display strings, blank before the first run.</summary>
public sealed partial class AuditViewModel : AuditStatusBase
{
    /// <summary>Works out the Remaining time and the recent rate behind it.</summary>
    private readonly RemainingEstimator _remainingEstimator = new();

    /// <summary>Gets or sets the number of files checked so far, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Checked { get; set; } = string.Empty;

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Elapsed { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the files checked per second over the whole run, as a whole number with thousands separators, followed while the run is under way and the warm-up has passed
    ///     by the recent figure in parentheses; blank until a full second has elapsed. The recent figure is the rate behind Remaining, which counts skipped rows as well as checked ones.
    /// </summary>
    [ ObservableProperty ]
    public partial string FilesPerSecond { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of missing files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Missing { get; set; } = string.Empty;

    /// <summary>Gets or sets the progress-bar position, from 0 to 100.</summary>
    [ ObservableProperty ]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the estimated remaining time as <c>hh:mm:ss</c>, or the placeholder <c>Calculating...</c> while the rows are counted and during the warm-up.</summary>
    [ ObservableProperty ]
    public partial string Remaining { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of skipped rows, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Skipped { get; set; } = string.Empty;

    /// <summary>Gets or sets the total number of files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string TotalFiles { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of unverified files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unverified { get; set; } = string.Empty;

    /// <inheritdoc />
    public override void Reset()
    {
        _remainingEstimator.Reset();

        TotalFiles     = string.Empty;
        Checked        = string.Empty;
        Missing        = string.Empty;
        Unverified     = string.Empty;
        Skipped        = string.Empty;
        Elapsed        = string.Empty;
        Remaining      = string.Empty;
        FilesPerSecond = string.Empty;
        Percent        = 0;
        StatusPercent  = string.Empty;
    }

    /// <summary>Refreshes the statistics from a running audit.</summary>
    /// <param name="progress">The audit's live counters.</param>
    /// <param name="elapsed">The time since the run started.</param>
    /// <param name="isFinished">Whether the run has ended, which makes an empty table read 100%.</param>
    public void Update(AuditProgress progress, TimeSpan elapsed, bool isFinished)
    {
        // Checked and Skipped are each read once, so that everything shown from this refresh agrees: Checked + Skipped never exceeds Total.
        long done    = progress.Checked;
        long skipped = progress.Skipped;
        long handled = done + skipped;
        long total   = Math.Max(progress.TotalFiles, handled);

        // The displayed rate is over the whole run, the span the Elapsed box shows; Remaining uses the recent checking-time rate, which excludes the counting phase.
        double wholeRunSeconds = elapsed.TotalSeconds;

        TotalFiles     = progress.HasTotal ? total.ToString(format: "N0", CultureInfo.CurrentCulture) : string.Empty;
        Checked        = done.ToString(format: "N0", CultureInfo.CurrentCulture);
        Missing        = progress.Missing.ToString(format: "N0", CultureInfo.CurrentCulture);
        Unverified     = progress.Unverified.ToString(format: "N0", CultureInfo.CurrentCulture);
        Skipped        = skipped.ToString(format: "N0", CultureInfo.CurrentCulture);
        Elapsed        = TimeText.Format(elapsed.TotalSeconds);
        Remaining      = _remainingEstimator.Next(handled, total, progress.CheckingElapsed, progress.HasTotal, isFinished);
        FilesPerSecond = wholeRunSeconds >= 1 ? RecentRate.Format(done / wholeRunSeconds, isFinished ? null : _remainingEstimator.RecentRate) : string.Empty;

        Percent       = total > 0 ? Math.Min(val1: 100, handled * 100.0 / total) : isFinished && progress.HasTotal ? 100 : 0;
        StatusPercent = progress.HasTotal ? $"{Percent:0}%" : string.Empty;
    }
}
