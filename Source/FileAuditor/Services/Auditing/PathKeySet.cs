namespace FileAuditor.Services.Auditing;

/// <summary>The paths (or path and ID pairs) the Missing Files audit has seen, kept as 128-bit hashes so that millions of rows cost about 48 bytes each.</summary>
/// <remarks>
///     A key is the MD5 hash of the path with trailing spaces (U+0020 only) removed and upper-cased in the invariant culture, followed, when the audit has an ID column, by the ID
///     as it was read. Case is ignored because Windows paths are case-insensitive; trailing spaces are ignored because Windows strips them from a path. Nothing else is trimmed, so
///     a path with a leading space or a trailing tab is its own key and is checked. The path's length and a flag for the ID are hashed ahead of the text so that no two different
///     (path, ID) pairs share the hashed text (<c>"a"</c> with <c>"bc"</c> against <c>"ab"</c> with <c>"c"</c>). MD5 is used for its speed and even spread, not for security; at
///     128 bits a collision is negligible. <see cref="Add" /> must be called from one thread. <see cref="IndexPathSet" /> trims both ends, so it cannot serve here.
/// </remarks>
public sealed class PathKeySet
{
    /// <summary>The characters hashed ahead of the text: the path's length as two characters, and the ID flag.</summary>
    private const int PrefixLength = 3;

    /// <summary>The 128-bit keys seen so far.</summary>
    private readonly HashSet<UInt128> _keys = [];

    /// <summary>Adds a path (and ID) to the set.</summary>
    /// <param name="path">The path as read from the index.</param>
    /// <param name="id">The ID as read, or <see langword="null" /> when there is no ID column or the value is <c>NULL</c>.</param>
    /// <param name="hasIdColumn">Whether the audit has an ID column, which makes the ID part of the key.</param>
    /// <returns><see langword="true" /> when the key was new; <see langword="false" /> when it had been seen before.</returns>
    public bool Add(string path, string? id, bool hasIdColumn)
    {
        ReadOnlySpan<char> trimmed  = path.AsSpan()
                                          .TrimEnd(trimChar: ' ');
        int                idLength = hasIdColumn ? id?.Length ?? 0 : 0;
        int                length   = PrefixLength + trimmed.Length + idLength;

        char[] rented = ArrayPool<char>.Shared.Rent(length);

        try
        {
            rented[0] = (char)(trimmed.Length & 0xFFFF);
            rented[1] = (char)(trimmed.Length >>> 16);
            rented[2] = hasIdColumn && id is not null ? '\u0001' : '\u0000';

            trimmed.ToUpperInvariant(rented.AsSpan(PrefixLength, trimmed.Length));

            if (idLength > 0)
            {
                id.AsSpan()
                  .CopyTo(rented.AsSpan(PrefixLength + trimmed.Length));
            }

            Span<byte> digest = stackalloc byte[16];

            MD5.HashData(MemoryMarshal.AsBytes(rented.AsSpan(start: 0, length)), digest);

            return _keys.Add(MemoryMarshal.Read<UInt128>(digest));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }
}
