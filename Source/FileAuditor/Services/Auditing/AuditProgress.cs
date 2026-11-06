namespace FileAuditor.Services.Auditing;

/// <summary>
///     The live statistics of a running audit. The runner updates the counters from many threads with <see cref="Interlocked" />; the UI polls them.
/// </summary>
public sealed class AuditProgress
{
    private long _total;

    private long _checked;

    private long _missing;

    private long _unverified;

    private volatile string _phase = "";

    private volatile Stopwatch? _checkingClock;

    /// <summary>Gets the table's row count; meaningful once <see cref="HasTotal" /> is <see langword="true" />.</summary>
    public long TotalFiles => Interlocked.Read(ref _total);

    /// <summary>Gets a value indicating whether the row count is known yet.</summary>
    public bool HasTotal => _checkingClock is not null;

    /// <summary>Gets the number of rows checked so far.</summary>
    public long Checked => Interlocked.Read(ref _checked);

    /// <summary>Gets the number of missing files found so far.</summary>
    public long Missing => Interlocked.Read(ref _missing);

    /// <summary>Gets the number of unverified files found so far.</summary>
    public long Unverified => Interlocked.Read(ref _unverified);

    /// <summary>Gets or sets the status-bar text for the current phase.</summary>
    public string Phase
    {
        get => _phase;
        set => _phase = value;
    }

    /// <summary>Gets the time spent checking files, which is the basis of the rate; zero until the row count is known.</summary>
    public TimeSpan CheckingElapsed => _checkingClock?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Records the row count and starts the checking clock.</summary>
    /// <param name="total">The table's row count.</param>
    public void SetTotal(long total)
    {
        Interlocked.Exchange(ref _total, total);
        _checkingClock = Stopwatch.StartNew();
    }

    /// <summary>Records one checked row.</summary>
    /// <param name="outcome">The row's outcome.</param>
    public void Record(CheckOutcome outcome)
    {
        switch (outcome)
        {
            case CheckOutcome.Found:
                break;
            case CheckOutcome.MissingEmpty or CheckOutcome.MissingDoesNotExist:
                Interlocked.Increment(ref _missing);

                break;
            default:
                Interlocked.Increment(ref _unverified);

                break;
        }

        Interlocked.Increment(ref _checked);
    }
}
