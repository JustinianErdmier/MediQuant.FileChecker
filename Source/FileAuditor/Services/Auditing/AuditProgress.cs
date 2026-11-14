namespace FileAuditor.Services.Auditing;

/// <summary>The live statistics of a running audit. The runner updates the counters from several threads with <see cref="Interlocked" />; the UI polls them.</summary>
public sealed class AuditProgress
{
    /// <summary>The number of duplicate rows read so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _duplicates;

    /// <summary>The number of empty-path rows read so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _emptyPaths;

    /// <summary>The number of different real paths first seen so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _distinct;

    /// <summary>The number of distinct paths whose check has ended. Read and written with <see cref="Interlocked" />.</summary>
    private long _checked;

    /// <summary>The number of files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _found;

    /// <summary>Times the checking phase; <see langword="null" /> until the row count is known.</summary>
    private volatile Stopwatch? _checkingClock;

    /// <summary>The number of missing files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _missing;

    /// <summary>The status-bar text for the current phase.</summary>
    private volatile string _phase = "";

    /// <summary>The number of skip-marker rows read so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _skipped;

    /// <summary>The table's row count; zero until counting ends. Read and written with <see cref="Interlocked" />.</summary>
    private long _totalRows;

    /// <summary>The number of unverified files found so far. Read and written with <see cref="Interlocked" />.</summary>
    private long _unverified;

    /// <summary>Gets the number of distinct paths whose check has ended: Found + Missing + Unverified, without the empty paths.</summary>
    public long Checked => Interlocked.Read(ref _checked);

    /// <summary>Gets the time spent since the row count became known, which is the basis of the rates; zero until then.</summary>
    public TimeSpan CheckingElapsed => _checkingClock?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Gets the number of different real paths first seen so far; it never goes down.</summary>
    public long Distinct => Interlocked.Read(ref _distinct);

    /// <summary>Gets the number of rows read so far whose path had already been seen, each counted once.</summary>
    public long Duplicates => Interlocked.Read(ref _duplicates);

    /// <summary>Gets the number of rows read so far whose path is <c>NULL</c> or only white space.</summary>
    public long EmptyPaths => Interlocked.Read(ref _emptyPaths);

    /// <summary>Gets the number of files found so far.</summary>
    public long Found => Interlocked.Read(ref _found);

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

    /// <summary>
    ///     Gets the number of rows handled so far: every skipped, empty and duplicate row (handled when read) and every distinct row whose check has ended. The progress bar
    ///     and the remaining time run on this figure out of <see cref="TotalRows" />.
    /// </summary>
    public long RowsHandled => Checked + Duplicates + EmptyPaths + Skipped;

    /// <summary>Gets the number of rows read so far whose path is a skip marker.</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>Gets the table's row count, fixed once counting ends; zero until then.</summary>
    public long TotalRows => Interlocked.Read(ref _totalRows);

    /// <summary>Gets the number of unverified files found so far.</summary>
    public long Unverified => Interlocked.Read(ref _unverified);

    /// <summary>Records the table's row count and starts the checking clock.</summary>
    /// <param name="rows">The number of rows in the table.</param>
    public void SetTotal(long rows)
    {
        Interlocked.Exchange(ref _totalRows, rows);

        _checkingClock = Stopwatch.StartNew();
    }

    /// <summary>Records a row whose path is a skip marker.</summary>
    public void AddSkipped() => Interlocked.Increment(ref _skipped);

    /// <summary>Records a row whose path is empty.</summary>
    public void AddEmptyPath() => Interlocked.Increment(ref _emptyPaths);

    /// <summary>Records a row whose path (or path and ID pair) had already been seen.</summary>
    public void AddDuplicate() => Interlocked.Increment(ref _duplicates);

    /// <summary>Records a real path seen for the first time.</summary>
    public void AddDistinct() => Interlocked.Increment(ref _distinct);

    /// <summary>Records one checked distinct path.</summary>
    /// <param name="outcome">
    ///     The path's outcome. A <see cref="CheckOutcomes.Skipped" /> outcome should never arrive, because the stream excludes skip rows; it is recorded as Unverified.
    /// </param>
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
