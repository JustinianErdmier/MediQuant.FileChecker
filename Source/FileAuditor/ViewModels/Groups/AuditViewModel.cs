namespace FileAuditor.ViewModels.Groups;

/// <summary>The Audit group and status bar: read-only run statistics as display strings, blank before the first run.</summary>
public sealed partial class AuditViewModel : AuditStatusBase
{
    /// <summary>Works out the Remaining time from the rows handled.</summary>
    private readonly RemainingEstimator _remainingEstimator = new();

    /// <summary>The recent rate of files checked, the bracketed figure of Files/second; separate from the rate behind Remaining, which counts every row handled.</summary>
    private readonly RecentRate _filesRate = new();

    /// <summary>Gets or sets the number of files checked so far, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Checked { get; set; } = string.Empty;

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Elapsed { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the files checked per second over the whole run, as a whole number with thousands separators, followed while the run is under way and the warm-up has passed
    ///     by the recent figure in parentheses; blank until a full second has elapsed. Both count distinct files checked.
    /// </summary>
    [ ObservableProperty ]
    public partial string FilesPerSecond { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of different real paths first seen so far, formatted with thousands separators; blank until the counting ends.</summary>
    [ ObservableProperty ]
    public partial string Distinct { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of duplicate rows read so far, formatted with thousands separators; blank until the counting ends.</summary>
    [ ObservableProperty ]
    public partial string Duplicates { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of files found, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Found { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of missing files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Missing { get; set; } = string.Empty;

    /// <summary>Gets or sets the progress-bar position, from 0 to 100.</summary>
    [ ObservableProperty ]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the estimated remaining time as <c>hh:mm:ss</c>, or the placeholder <c>Calculating...</c> while the rows are counted and during the warm-up.</summary>
    [ ObservableProperty ]
    public partial string Remaining { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of skipped rows read so far, formatted with thousands separators; blank until the counting ends.</summary>
    [ ObservableProperty ]
    public partial string Skipped { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the table's row count, fixed once the counting ends; blank until then.
    /// </summary>
    [ ObservableProperty ]
    public partial string TotalFiles { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the number of unverified files, followed by the number of empty paths read so far in parentheses when there are any.
    /// </summary>
    [ ObservableProperty ]
    public partial string Unverified { get; set; } = string.Empty;

    /// <inheritdoc />
    public override void Reset()
    {
        _remainingEstimator.Reset();
        _filesRate.Reset();

        TotalFiles     = string.Empty;
        Distinct       = string.Empty;
        Duplicates     = string.Empty;
        Checked        = string.Empty;
        Found          = string.Empty;
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
        // The workers record an outcome before they count the file as checked, and the reader counts a path as distinct before it is checked. Found, Missing and Unverified are
        // therefore read once and Checked is their sum, and Distinct is read last (it only grows) and never shown below Checked, so that everything shown from this refresh agrees.
        long found      = progress.Found;
        long missing    = progress.Missing;
        long unverified = progress.Unverified;
        long done       = found + missing + unverified;
        long duplicates = progress.Duplicates;
        long emptyPaths = progress.EmptyPaths;
        long skipped    = progress.Skipped;
        long distinct   = Math.Max(progress.Distinct, done);
        long total      = progress.TotalRows;
        bool hasCounts  = progress.HasTotal;
        long handled    = done + duplicates + emptyPaths + skipped;

        // The displayed rate is over the whole run, the span the Elapsed box shows; both recent rates use the checking-time clock, which excludes the counting phase.
        double wholeRunSeconds = elapsed.TotalSeconds;
        double? recentFiles = hasCounts && !isFinished ? _filesRate.Next(done, progress.CheckingElapsed) : null;

        TotalFiles = hasCounts ? Format(total) : string.Empty;
        Distinct   = hasCounts ? Format(distinct) : string.Empty;
        Duplicates = hasCounts ? Format(duplicates) : string.Empty;
        Skipped    = hasCounts ? Format(skipped) : string.Empty;
        Checked    = Format(done);
        Found      = Format(found);
        Missing    = Format(missing);
        Unverified = WithParenthesis(unverified, emptyPaths, show: emptyPaths > 0);
        Elapsed    = TimeText.Format(elapsed.TotalSeconds);
        Remaining  = _remainingEstimator.Next(handled, total, progress.CheckingElapsed, hasCounts, isFinished);

        FilesPerSecond = wholeRunSeconds >= 1 ? RecentRate.Format(done / wholeRunSeconds, recentFiles) : string.Empty;

        Percent       = total > 0 ? Math.Min(100, handled * 100.0 / total) : isFinished && hasCounts ? 100 : 0;
        StatusPercent = hasCounts ? $"{Percent:0}%" : string.Empty;
    }
    /// <summary>Formats a count with thousands separators.</summary>
    private static string Format(long value) => value.ToString(format: "N0", CultureInfo.CurrentCulture);

    /// <summary>Formats a count, followed by a second count in parentheses when <paramref name="show" /> is set.</summary>
    private static string WithParenthesis(long value, long inParentheses, bool show) => show ? $"{Format(value)} ({Format(inParentheses)})" : Format(value);
}
