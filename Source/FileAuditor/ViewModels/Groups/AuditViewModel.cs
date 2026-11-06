namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     The Audit group and status bar: read-only run statistics as display strings, blank before the first run.
/// </summary>
public sealed partial class AuditViewModel : ObservableObject
{
    /// <summary>Gets or sets the total number of files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string TotalFiles { get; set; } = "";

    /// <summary>Gets or sets the number of files checked so far, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Checked { get; set; } = "";

    /// <summary>Gets or sets the number of missing files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Missing { get; set; } = "";

    /// <summary>Gets or sets the number of unverified files, formatted with thousands separators.</summary>
    [ ObservableProperty ]
    public partial string Unverified { get; set; } = "";

    /// <summary>Gets or sets the elapsed time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Elapsed { get; set; } = "";

    /// <summary>Gets or sets the estimated remaining time as <c>hh:mm:ss</c>.</summary>
    [ ObservableProperty ]
    public partial string Remaining { get; set; } = "";

    /// <summary>Gets or sets the checking rate, to two decimal places.</summary>
    [ ObservableProperty ]
    public partial string FilesPerSecond { get; set; } = "";

    /// <summary>Gets or sets the progress-bar position, from 0 to 100.</summary>
    [ ObservableProperty ]
    public partial double Percent { get; set; }

    /// <summary>Gets or sets the status-bar message.</summary>
    [ ObservableProperty ]
    public partial string StatusMessage { get; set; } = "Ready";

    /// <summary>Gets or sets the status-bar percentage text, blank when no run is under way.</summary>
    [ ObservableProperty ]
    public partial string StatusPercent { get; set; } = "";

    /// <summary>Blanks every statistic and the progress bar, ready for a new run.</summary>
    public void Reset()
    {
        TotalFiles = "";
        Checked = "";
        Missing = "";
        Unverified = "";
        Elapsed = "";
        Remaining = "";
        FilesPerSecond = "";
        Percent = 0;
        StatusPercent = "";
    }

    /// <summary>Refreshes the statistics from a running audit.</summary>
    /// <param name="progress">The audit's live counters.</param>
    /// <param name="elapsed">The time since the run started.</param>
    /// <param name="isFinished">Whether the run has ended, which makes an empty table read 100%.</param>
    public void Update(AuditProgress progress, TimeSpan elapsed, bool isFinished)
    {
        long total = progress.TotalFiles;
        long done = progress.Checked;
        double seconds = progress.CheckingElapsed.TotalSeconds;
        double rate = seconds > 0 ? done / seconds : 0;

        TotalFiles = progress.HasTotal ? total.ToString("N0", CultureInfo.CurrentCulture) : "";
        Checked = done.ToString("N0", CultureInfo.CurrentCulture);
        Missing = progress.Missing.ToString("N0", CultureInfo.CurrentCulture);
        Unverified = progress.Unverified.ToString("N0", CultureInfo.CurrentCulture);
        Elapsed = FormatTime(elapsed);
        Remaining = FormatTime(rate > 0 && total > done ? TimeSpan.FromSeconds((total - done) / rate) : TimeSpan.Zero);
        FilesPerSecond = rate.ToString("0.00", CultureInfo.CurrentCulture);

        Percent = total > 0 ? Math.Min(100, done * 100.0 / total) : isFinished && progress.HasTotal ? 100 : 0;
        StatusPercent = progress.HasTotal ? $"{Percent:0}%" : "";
    }

    private static string FormatTime(TimeSpan time) => $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}
