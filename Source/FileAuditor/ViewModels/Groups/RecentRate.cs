namespace FileAuditor.ViewModels.Groups;

/// <summary>
///     A recent-weighted rate of a running count, in items per second: an exponentially weighted moving average of the per-sample rate, so it follows the last
///     <see cref="TimeConstantSeconds" /> seconds or so. It is <see langword="null" /> until the warm-up has passed. Not a view model: it has no view.
/// </summary>
public sealed class RecentRate
{
    /// <summary>
    ///     The averaging time constant in seconds. Thirty seconds is long enough to smooth the second-to-second noise of a network share and short enough to follow a
    ///     real change in speed within a minute.
    /// </summary>
    public const double TimeConstantSeconds = 30;

    /// <summary>
    ///     The warm-up in seconds. A rate taken over the first few seconds is dominated by connection set-up and cache warming, so none is reported until this
    ///     much time has passed; the average then starts from the whole warm-up's rate.
    /// </summary>
    public const double WarmUpSeconds = 5;

    private long _previousCount;

    private double _previousSeconds;

    private bool _seeded;

    /// <summary>Gets the rate from the latest <see cref="Next" /> call, or <see langword="null" /> during the warm-up.</summary>
    public double? Value { get; private set; }

    /// <summary>Forgets every sample, so the next run starts its own warm-up.</summary>
    public void Reset()
    {
        _previousCount   = 0;
        _previousSeconds = 0;
        _seeded          = false;
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
            if (seconds < WarmUpSeconds)
            {
                return null;
            }

            _seeded = true;
            Value   = count / seconds;
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
}