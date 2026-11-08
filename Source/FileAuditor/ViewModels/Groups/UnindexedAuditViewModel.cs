namespace FileAuditor.ViewModels.Groups;

/// <summary>The Audit group of the Unindexed Files tab: read-only run statistics as display strings, blank before the first run. It has no Remaining time.</summary>
public sealed partial class UnindexedAuditViewModel : AuditStatusBase
{
    private const double MarqueeStep = 5;

    // The marquee block is MarqueeWidth of 100 units wide and moves MarqueeStep units on each refresh, so with four refreshes a second it crosses in four seconds.
    private const double MarqueeWidth = 20;

    private readonly RecentRate _recentRate = new();

    private double _marqueePosition;

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Elapsed { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the files found per second over the whole run, as a whole number with thousands separators, followed while the run is under way and the warm-up has passed by
    ///     the recent figure in parentheses; blank until a full second has elapsed. The recent figure is timed from the start of the scan.
    /// </summary>
    [ ObservableProperty ]
    public partial string FilesPerSecond { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of files found that are in the index, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Indexed { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether a run is under way, which shows the moving block in place of the progress bar.</summary>
    [ ObservableProperty ]
    public partial bool IsRunning { get; set; }

    /// <summary>Gets or sets the width, in star units, of the gap after the moving block.</summary>
    [ ObservableProperty ]
    public partial GridLength MarqueeAfter { get; set; } = new(100 - MarqueeWidth, GridUnitType.Star);

    /// <summary>Gets or sets the width, in star units, of the gap before the moving block.</summary>
    [ ObservableProperty ]
    public partial GridLength MarqueeBefore { get; set; } = new(value: 0, GridUnitType.Star);

    /// <summary>Gets the width, in star units, of the moving block.</summary>
    public GridLength MarqueeBlock { get; } = new(MarqueeWidth, GridUnitType.Star);

    /// <summary>Gets or sets the progress-bar position: full when a run has ended, otherwise empty; a running audit shows the moving block instead.</summary>
    [ ObservableProperty ]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the number of files found so far, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string TotalFiles { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of files found that are not in the index, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unindexed { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of folders that could not be listed, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unverified { get; set; } = string.Empty;

    /// <inheritdoc />
    public override void Reset()
    {
        _recentRate.Reset();

        TotalFiles     = string.Empty;
        Indexed        = string.Empty;
        Unindexed      = string.Empty;
        Unverified     = string.Empty;
        Elapsed        = string.Empty;
        FilesPerSecond = string.Empty;
        Percent        = 0;
        StatusPercent  = string.Empty;

        SetMarquee(position: 0);

        IsRunning = false;
    }

    /// <summary>Shows the moving block, for the moment a run begins and nothing has been counted yet.</summary>
    public void Begin()
    {
        SetMarquee(position: 0);

        IsRunning = true;
    }

    /// <summary>Refreshes the statistics from a running audit.</summary>
    /// <param name="progress">The audit's live counters.</param>
    /// <param name="elapsed">The time since the run started.</param>
    /// <param name="isFinished">Whether the run has ended, which fills the progress bar; otherwise each refresh moves the block on.</param>
    public void Update(UnindexedProgress progress, TimeSpan elapsed, bool isFinished)
    {
        long    total  = progress.TotalFiles;
        double? recent = _recentRate.Next(total, progress.ScanElapsed);

        TotalFiles     = total.ToString(format: "N0", CultureInfo.CurrentCulture);
        Indexed        = progress.Indexed.ToString(format: "N0", CultureInfo.CurrentCulture);
        Unindexed      = progress.Unindexed.ToString(format: "N0", CultureInfo.CurrentCulture);
        Unverified     = progress.Unverified.ToString(format: "N0", CultureInfo.CurrentCulture);
        Elapsed        = TimeText.Format(elapsed.TotalSeconds);
        FilesPerSecond = elapsed.TotalSeconds >= 1 ? RecentRate.Format(total / elapsed.TotalSeconds, isFinished ? null : recent) : string.Empty;

        IsRunning     = !isFinished;
        Percent       = isFinished ? 100 : 0;
        StatusPercent = isFinished ? "100%" : string.Empty;

        if (!isFinished)
        {
            // The block runs off the right-hand end and starts again from the left.
            SetMarquee(_marqueePosition + MarqueeStep > 100 - MarqueeWidth ? 0 : _marqueePosition + MarqueeStep);
        }
    }

    private void SetMarquee(double position)
    {
        _marqueePosition = position;

        MarqueeBefore = new GridLength(position, GridUnitType.Star);
        MarqueeAfter  = new GridLength(100 - MarqueeWidth - position, GridUnitType.Star);
    }
}
