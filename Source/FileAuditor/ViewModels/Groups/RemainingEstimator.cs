namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     Turns the audit's progress samples into the Remaining text: an estimate from the <see cref="RecentRate" /> that the display eases toward while counting
///     down one second per second, so it neither holds still nor jumps. Not a view model: it has no view.
/// </summary>
public sealed class RemainingEstimator
{
    /// <summary>The placeholder shown until there is a rate to estimate from.</summary>
    public const string CalculatingText = "Calculating...";

    /// <summary>
    ///     The easing time constant in seconds. Three seconds moves the display most of the way to a new estimate within a few refreshes' worth of seconds, yet
    ///     smooths the estimate's own wobble enough that the digits read as a clock.
    /// </summary>
    public const double EasingSeconds = 3;

    private const string Finished = "00:00:00";

    private readonly RecentRate _rate = new();

    private double _lastSeconds;

    private double? _shown;

    /// <summary>Gets the recent rate in rows per second, or <see langword="null" /> during the warm-up.</summary>
    public double? RecentRate => _rate.Value;

    /// <summary>Forgets the previous run.</summary>
    public void Reset()
    {
        _rate.Reset();

        _lastSeconds = 0;
        _shown       = null;
    }

    /// <summary>Takes a sample and returns the Remaining text.</summary>
    /// <param name="processed">The rows processed so far, checked and skipped.</param>
    /// <param name="total">The total rows, the on-screen total, never less than <paramref name="processed" />.</param>
    /// <param name="checkingElapsed">The time spent checking, which excludes counting the rows.</param>
    /// <param name="hasTotal">Whether the row count is known yet.</param>
    /// <param name="isFinished">Whether the run has ended.</param>
    /// <returns>The remaining time as <c>hh:mm:ss</c>, or <see cref="CalculatingText" /> during the warm-up.</returns>
    public string Next(long processed, long total, TimeSpan checkingElapsed, bool hasTotal, bool isFinished)
    {
        if (isFinished)
        {
            return Finished;
        }

        if (!hasTotal)
        {
            return CalculatingText;
        }

        double? rate = _rate.Next(processed, checkingElapsed);

        if (rate is null)
        {
            return CalculatingText;
        }

        double seconds = checkingElapsed.TotalSeconds;
        double step    = Math.Max(val1: 0, seconds - _lastSeconds);

        _lastSeconds = seconds;

        if (processed >= total)
        {
            _shown = 0;

            return Finished;
        }

        // A stalled run has no rate to divide by; keep the last shown value, counting down, rather than jump to infinity.
        double? target = rate > 0 ? (total - processed) / rate : null;

        if (_shown is null)
        {
            if (target is null)
            {
                return CalculatingText;
            }

            _shown = target;
        }
        else
        {
            double shown = Math.Max(val1: 0, _shown.Value - step);

            if (target is not null)
            {
                shown += (target.Value - shown) * (1 - Math.Exp(-step / EasingSeconds));
            }

            _shown = Math.Max(val1: 0, shown);
        }

        return FormatTime(TimeSpan.FromSeconds(_shown.Value));
    }

    private static string FormatTime(TimeSpan time) => $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
}