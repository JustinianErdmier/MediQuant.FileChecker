namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     Turns the audit's progress samples into the Remaining text: an estimate from the <see cref="RecentRate" /> that the display eases toward while counting down one second
///     per second, so it neither holds still nor jumps. Not a view model: it has no view.
/// </summary>
public sealed class RemainingEstimator
{
    /// <summary>The text shown once the run has finished.</summary>
    private const string Finished = "00:00:00";

    /// <summary>
    ///     How long, in seconds of checking time, the processed count must stand still before the display holds. One refresh is 250 ms, and a single quiet refresh is the ordinary
    ///     jitter on a slow share, not a stall.
    /// </summary>
    private const double StallHoldSeconds = 1;

    /// <summary>The placeholder shown until there is a rate to estimate from.</summary>
    public const string CalculatingText = "Calculating...";

    /// <summary>
    ///     The easing time constant in seconds. Three seconds moves the display most of the way to a new estimate within a few refreshes' worth of seconds, yet smooths the
    ///     estimate's own wobble enough that the digits read as a clock.
    /// </summary>
    public const double EasingSeconds = 3;

    /// <summary>The recent rate the estimate is made from.</summary>
    private readonly RecentRate _rate = new();

    /// <summary>The checking time at which the processed count last changed, in seconds.</summary>
    private double _changedSeconds;

    /// <summary>The processed count at the previous sample; -1 before the first one.</summary>
    private long _lastProcessed = -1;

    /// <summary>The checking time of the previous sample, in seconds.</summary>
    private double _lastSeconds;

    /// <summary>The remaining time on display, in seconds; <see langword="null" /> until there is an estimate.</summary>
    private double? _shown;

    /// <summary>Gets the recent rate in rows per second, or <see langword="null" /> during the warm-up.</summary>
    public double? RecentRate => _rate.Value;

    /// <summary>Forgets the previous run.</summary>
    public void Reset()
    {
        _rate.Reset();

        _changedSeconds = 0;
        _lastSeconds    = 0;
        _lastProcessed  = -1;
        _shown          = null;
    }

    /// <summary>Takes a sample and returns the Remaining text.</summary>
    /// <param name="processed">The distinct paths checked so far.</param>
    /// <param name="total">The distinct paths to check, the audit's <c>Total files</c> figure.</param>
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

        if (processed != _lastProcessed)
        {
            _changedSeconds = seconds;
        }

        _lastSeconds   = seconds;
        _lastProcessed = processed;

        if (processed >= total)
        {
            _shown = 0;

            return Finished;
        }

        // A stalled run has no progress to extrapolate from: once the count has stood still for a second, hold the display where it is (the rate keeps decaying
        // underneath) rather than count down to zero.
        if (_shown is not null
            && seconds - _changedSeconds >= StallHoldSeconds)
        {
            return TimeText.Format(_shown.Value);
        }

        double? target = rate > 0 ? (total - processed) / rate : null;

        if (_shown is null)
        {
            if (target is null)
            {
                return CalculatingText;
            }

            _shown = Math.Min(TimeText.MaxSeconds, target.Value);
        }
        else if (target is not null)
        {
            double shown = Math.Max(val1: 0, _shown.Value - step);

            shown += (target.Value - shown) * (1 - Math.Exp(-step / EasingSeconds));

            _shown = Math.Clamp(shown, min: 0, TimeText.MaxSeconds);
        }

        return TimeText.Format(_shown.Value);
    }
}
