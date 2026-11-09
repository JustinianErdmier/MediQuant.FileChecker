namespace FileAuditor.Services.Auditing;

/// <summary>The live statistics of a running audit. The runner updates the counters from many threads with <see cref="Interlocked" />; the UI polls them.</summary>
public sealed class AuditProgress
{
    /// <summary>The number of rows checked so far, not counting skipped rows. Read and written with <see cref="Interlocked" />.</summary>
    private long _checked;

    /// <summary>Times the checking phase; <see langword="null" /> until the row count is known.</summary>
    private volatile Stopwatch? _checkingClock;

    /// <summary>The number of missing files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _missing;

    /// <summary>The status-bar text for the current phase.</summary>
    private volatile string _phase = "";

    /// <summary>The number of rows skipped so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _skipped;

    /// <summary>The table's row count. Read and written with <see cref="Interlocked" />.</summary>
    private long _total;

    /// <summary>The number of unverified files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _unverified;

    /// <summary>Gets the number of rows checked so far.</summary>
    public long Checked => Interlocked.Read(ref _checked);

    /// <summary>Gets the time spent checking files, which is the basis of the rate; zero until the row count is known.</summary>
    public TimeSpan CheckingElapsed => _checkingClock?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Gets a value indicating whether the row count is known yet.</summary>
    public bool HasTotal => _checkingClock is not null;

    /// <summary>Gets the number of missing files found so far.</summary>
    public long Missing => Interlocked.Read(ref _missing);

    /// <summary>Gets or sets the status-bar text for the current phase.</summary>
    public string Phase
    {
        get => _phase;

        set => _phase = value;
    }

    /// <summary>Gets the number of rows skipped so far because their path value was a skip marker; they are not part of <see cref="Checked" />.</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>Gets the table's row count; meaningful once <see cref="HasTotal" /> is <see langword="true" />.</summary>
    public long TotalFiles => Interlocked.Read(ref _total);

    /// <summary>Gets the number of unverified files found so far.</summary>
    public long Unverified => Interlocked.Read(ref _unverified);

    /// <summary>Records the row count and starts the checking clock.</summary>
    /// <param name="total">The table's row count.</param>
    public void SetTotal(long total)
    {
        Interlocked.Exchange(ref _total, total);
        _checkingClock = Stopwatch.StartNew();
    }

    /// <summary>Records one processed row: a checked one, or a skipped one.</summary>
    /// <param name="outcome">The row's outcome.</param>
    public void Record(CheckOutcomes outcome)
    {
        switch (outcome)
        {
            case CheckOutcomes.Skipped:
                Interlocked.Increment(ref _skipped);

                return;

            case CheckOutcomes.Found:
                break;

            case CheckOutcomes.MissingEmpty or CheckOutcomes.MissingDoesNotExist:
                Interlocked.Increment(ref _missing);

                break;

            case CheckOutcomes.UnverifiedDriveOrShareNotFound
                 or CheckOutcomes.UnverifiedPermissionDenied
                 or CheckOutcomes.UnverifiedNetworkUnreachable
                 or CheckOutcomes.UnverifiedPathTooLong
                 or CheckOutcomes.UnverifiedLinkNotFollowed
                 or CheckOutcomes.UnverifiedInvalidCharacters
                 or CheckOutcomes.UnverifiedPathEmpty
                 or CheckOutcomes.UnverifiedOther:
                Interlocked.Increment(ref _unverified);

                break;

            default:
                throw new UnreachableException();
        }

        Interlocked.Increment(ref _checked);
    }
}
