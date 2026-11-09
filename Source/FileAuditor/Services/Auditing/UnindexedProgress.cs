namespace FileAuditor.Services.Auditing;

/// <summary>
///     The live statistics of a running unindexed-files audit. The runner updates the counters from many threads with <see cref="Interlocked" />; the UI polls them. The whole
///     run's clock is not kept here: the caller times it. Only the scan's clock is, because the rate of finding files must not count the index load.
/// </summary>
public sealed class UnindexedProgress
{
    /// <summary>The number of files found that are in the index. Read and written with <see cref="Interlocked" />.</summary>
    private long _indexed;

    /// <summary>The status-bar text for the current phase.</summary>
    private volatile string _phase = string.Empty;

    /// <summary>Times the folder scan; <see langword="null" /> until the scan begins.</summary>
    private volatile Stopwatch? _scanClock;

    /// <summary>The number of files found that are not in the index. Read and written with <see cref="Interlocked" />.</summary>
    private long _unindexed;

    /// <summary>The number of folders that could not be listed plus links not followed. Read and written with <see cref="Interlocked" />.</summary>
    private long _unverified;

    /// <summary>Gets the number of files found that are in the index.</summary>
    public long Indexed => Interlocked.Read(ref _indexed);

    /// <summary>Gets or sets the status-bar text for the current phase.</summary>
    public string Phase
    {
        get => _phase;

        set => _phase = value;
    }

    /// <summary>Gets the time spent scanning folders, which is the basis of the recent rate; zero until the scan begins, so the index load is not counted.</summary>
    public TimeSpan ScanElapsed => _scanClock?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Gets the number of files found so far: those in the index plus those that are not.</summary>
    public long TotalFiles => Indexed + Unindexed;

    /// <summary>Gets the number of files found that are not in the index.</summary>
    public long Unindexed => Interlocked.Read(ref _unindexed);

    /// <summary>Gets the number of folders that could not be listed plus links not followed.</summary>
    public long Unverified => Interlocked.Read(ref _unverified);

    /// <summary>Starts the scan clock. The runner calls it just before the folder walk begins, after the index is loaded.</summary>
    public void StartScan() => _scanClock = Stopwatch.StartNew();

    /// <summary>
    ///     Stops the scan clock. The runner calls it as soon as the folder walk ends, so writing the report is not counted as scan time; <see cref="ScanElapsed" /> stays readable.
    /// </summary>
    public void StopScan() => _scanClock?.Stop();

    /// <summary>Records one file that was checked against the index.</summary>
    /// <param name="indexed">Whether the file is in the index.</param>
    public void Record(bool indexed)
    {
        if (indexed)
        {
            Interlocked.Increment(ref _indexed);
        }
        else
        {
            Interlocked.Increment(ref _unindexed);
        }
    }

    /// <summary>Records one folder that could not be listed, or one link that was not followed.</summary>
    public void RecordUnverified() => Interlocked.Increment(ref _unverified);
}
