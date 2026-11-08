namespace FileAuditor.Services.Auditing;

/// <summary>
///     The live statistics of a running unindexed-files audit. The runner updates the counters from many threads with <see cref="Interlocked" />; the UI polls them. The
///     run's clock is not kept here: the caller times the whole run.
/// </summary>
public sealed class UnindexedProgress
{
    private long _indexed;

    private volatile string _phase = "";

    private long _unindexed;

    private long _unverified;

    /// <summary>Gets the number of files found that are in the index.</summary>
    public long Indexed => Interlocked.Read(ref _indexed);

    /// <summary>Gets or sets the status-bar text for the current phase.</summary>
    public string Phase
    {
        get => _phase;

        set => _phase = value;
    }

    /// <summary>Gets the number of files found so far: those in the index plus those that are not.</summary>
    public long TotalFiles => Indexed + Unindexed;

    /// <summary>Gets the number of files found that are not in the index.</summary>
    public long Unindexed => Interlocked.Read(ref _unindexed);

    /// <summary>Gets the number of folders that could not be listed plus links not followed.</summary>
    public long Unverified => Interlocked.Read(ref _unverified);

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
