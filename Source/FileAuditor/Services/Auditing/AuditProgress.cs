namespace FileAuditor.Services.Auditing;

/// <summary>The live statistics of a running audit. The runner updates the counters from many threads with <see cref="Interlocked" />; the UI polls them.</summary>
public sealed class AuditProgress
{
    /// <summary>The number of rows checked so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _checked;

    /// <summary>The up-front row counts; <see langword="null" /> until they are known.</summary>
    private volatile RowCounts? _counts;

    /// <summary>The number of files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _found;

    /// <summary>Times the checking phase; <see langword="null" /> until the row count is known.</summary>
    private volatile Stopwatch? _checkingClock;

    /// <summary>The number of missing files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _missing;

    /// <summary>The status-bar text for the current phase.</summary>
    private volatile string _phase = "";

    /// <summary>The number of unverified files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _unverified;

    /// <summary>Gets the number of rows checked so far.</summary>
    public long Checked => Interlocked.Read(ref _checked);

    /// <summary>Gets the up-front row counts, or <see langword="null" /> until they are known.</summary>
    public RowCounts? Counts => _counts;

    /// <summary>Gets the number of files found so far.</summary>
    public long Found => Interlocked.Read(ref _found);

    /// <summary>Gets the time spent checking files, which is the basis of the rate; zero until the row count is known.</summary>
    public TimeSpan CheckingElapsed => _checkingClock?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Gets a value indicating whether the row counts are known yet.</summary>
    public bool HasTotal => _checkingClock is not null;

    /// <summary>Gets the number of missing files found so far.</summary>
    public long Missing => Interlocked.Read(ref _missing);

    /// <summary>Gets or sets the status-bar text for the current phase.</summary>
    public string Phase
    {
        get => _phase;

        set => _phase = value;
    }

    /// <summary>Gets the number of unverified files found so far.</summary>
    public long Unverified => Interlocked.Read(ref _unverified);

    /// <summary>Records the up-front row counts and starts the checking clock.</summary>
    /// <param name="counts">How the table's rows divide into empty paths, skip markers, distinct real paths and duplicates.</param>
    public void SetCounts(RowCounts counts)
    {
        _counts        = counts;
        _checkingClock = Stopwatch.StartNew();
    }

    /// <summary>Records one checked row.</summary>
    /// <param name="outcome">The row's outcome. A <see cref="CheckOutcomes.Skipped" /> outcome should never arrive, because the stream excludes skip rows; it is recorded as Unverified.</param>
    public void Record(CheckOutcomes outcome)
    {
        switch (outcome)
        {
            case CheckOutcomes.Found:
                Interlocked.Increment(ref _found);

                break;

            case CheckOutcomes.MissingEmpty or CheckOutcomes.MissingDoesNotExist:
                Interlocked.Increment(ref _missing);

                break;

            case CheckOutcomes.Skipped
                 or CheckOutcomes.UnverifiedDriveOrShareNotFound
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
