namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     A recent-weighted rate of a running count, in items per second: an exponentially weighted moving average of the per-sample rate, so it follows the last
///     <see cref="TimeConstantSeconds" /> seconds or so. It is <see langword="null" /> until the warm-up has passed. Not a view model: it has no view.
/// </summary>
public sealed class RecentRate
{
    /// <summary>
    ///     The averaging time constant in seconds. Thirty seconds is long enough to smooth the second-to-second noise of a network share and short enough to follow a real change
    ///     in speed within a minute.
    /// </summary>
    public const double TimeConstantSeconds = 30;

    /// <summary>
    ///     The warm-up in seconds. A rate taken over the first few seconds is dominated by connection set-up and cache warming, so none is reported until this much time has
    ///     passed; the average then starts from the warm-up's second half.
    /// </summary>
    public const double WarmUpSeconds = 5;

    /// <summary>The count at the anchor sample, which the first rate is measured from.</summary>
    private long _anchorCount;

    /// <summary>The time of the anchor sample, in seconds.</summary>
    private double _anchorSeconds;

    /// <summary>Whether the anchor sample has been taken.</summary>
    private bool _hasAnchor;

    /// <summary>The count at the previous sample.</summary>
    private long _previousCount;

    /// <summary>The time of the previous sample, in seconds.</summary>
    private double _previousSeconds;

    /// <summary>Whether the average has been seeded, which ends the warm-up.</summary>
    private bool _seeded;

    /// <summary>Gets the rate from the latest <see cref="Next" /> call, or <see langword="null" /> during the warm-up.</summary>
    public double? Value { get; private set; }

    /// <summary>Forgets every sample, so the next run starts its own warm-up.</summary>
    public void Reset()
    {
        _previousCount   = 0;
        _previousSeconds = 0;
        _seeded          = false;
        _hasAnchor       = false;
        _anchorCount     = 0;
        _anchorSeconds   = 0;
        Value            = null;
    }

    /// <summary>Takes a sample and returns the updated rate.</summary>
    /// <param name="count">The running count so far.</param>
    /// <param name="elapsed">The time over which <paramref name="count" /> built up.</param>
    /// <returns>The recent rate in items per second, or <see langword="null" /> until the warm-up has passed.</returns>
    public double? Next(long count, TimeSpan elapsed)
    {
        double seconds = elapsed.TotalSeconds;

        if (!_seeded)
        {
            // The first sample in the warm-up's second half anchors the seed, so the slow start-up ramp does not drag it down.
            if (!_hasAnchor
                && seconds >= WarmUpSeconds / 2)
            {
                _hasAnchor     = true;
                _anchorCount   = count;
                _anchorSeconds = seconds;
            }

            if (seconds < WarmUpSeconds)
            {
                return null;
            }

            _seeded = true;
            Value   = _hasAnchor && seconds > _anchorSeconds ? (count - _anchorCount) / (seconds - _anchorSeconds) : count / seconds;
        }
        else
        {
            double step = seconds - _previousSeconds;

            // A sample that is not later than the last one carries no rate; keep what we have.
            if (step <= 0)
            {
                return Value;
            }

            double weight = 1 - Math.Exp(-step / TimeConstantSeconds);

            Value += weight * ((count - _previousCount) / step - Value);
        }

        _previousCount   = count;
        _previousSeconds = seconds;

        return Value;
    }

    /// <summary>Formats the Files/second text: the whole-run average, then the recent rate in parentheses, each a whole number with thousands separators.</summary>
    /// <param name="wholeRun">The whole-run average, in items per second.</param>
    /// <param name="recent">The recent rate, or <see langword="null" /> to show the whole-run average alone.</param>
    /// <returns>The text, such as <c>54,112 (60,001)</c> or <c>54,112</c>.</returns>
    public static string Format(double wholeRun, double? recent)
    {
        string wholeRunText = wholeRun.ToString(format: "N0", CultureInfo.CurrentCulture);

        return recent is null ? wholeRunText : $"{wholeRunText} ({recent.Value.ToString(format: "N0", CultureInfo.CurrentCulture)})";
    }
}
