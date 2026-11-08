namespace FileAuditor.Services.Scanning;

/// <summary>The paths of the index table, kept as 128-bit hashes so that millions of rows cost about 48 bytes each (measured, with the set) instead of about 200.</summary>
/// <remarks>
///     A path's key is the MD5 hash of the UTF-16 bytes of <c>path.Trim().ToUpperInvariant()</c>. MD5 is used for its speed and even spread, not for security. At 128 bits the
///     chance of two different paths sharing a key is negligible (about 10^-25 for ten million paths), so a file is never reported as unindexed because of a collision (spec anti-goal
///     1). A collision could only ever hide an unindexed file, and not in any realistic table. <see cref="Add" /> must be called from one thread; once loading has finished,
///     <see cref="Contains" /> is safe from any number of threads at once.
/// </remarks>
public sealed class IndexPathSet
{
    private const int StackLimit = 1024;

    private readonly HashSet<UInt128> _keys;

    /// <summary>Creates an empty set.</summary>
    /// <param name="capacity">The expected number of paths, to avoid growing the set repeatedly; 0 when unknown.</param>
    public IndexPathSet(int capacity = 0) => _keys = new HashSet<UInt128>(capacity);

    /// <summary>Gets the number of distinct paths added.</summary>
    public int Count => _keys.Count;

    /// <summary>Adds a path; not thread-safe.</summary>
    /// <param name="path">The path as stored in the index; surrounding whitespace and letter case are ignored.</param>
    public void Add(string path) => _keys.Add(Hash(path));

    /// <summary>Tests whether a path is in the set, ignoring letter case and surrounding whitespace; safe from many threads once loading has finished.</summary>
    /// <param name="path">The full path of a file on disk.</param>
    /// <returns><see langword="true" /> when the path was added.</returns>
    public bool Contains(string path) => _keys.Contains(Hash(path));

    private static UInt128 Hash(string path)
    {
        ReadOnlySpan<char> trimmed = path.AsSpan()
                                         .Trim();

        Span<byte> digest = stackalloc byte[16];

        if (trimmed.Length <= StackLimit)
        {
            Span<char> upper = stackalloc char[trimmed.Length];

            trimmed.ToUpperInvariant(upper);

            MD5.HashData(MemoryMarshal.AsBytes(upper), digest);
        }
        else
        {
            char[] rented = ArrayPool<char>.Shared.Rent(trimmed.Length);

            try
            {
                trimmed.ToUpperInvariant(rented);

                MD5.HashData(MemoryMarshal.AsBytes(rented.AsSpan(start: 0, trimmed.Length)), digest);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        return MemoryMarshal.Read<UInt128>(digest);
    }
}
