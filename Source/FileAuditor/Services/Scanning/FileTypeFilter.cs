namespace FileAuditor.Services.Scanning;

/// <summary>Decides which file type entry, if any, a file name belongs to: the blacklist wins, and overlaps go to the most specific whitelist entry.</summary>
public sealed class FileTypeFilter
{
    /// <summary>Contains the compiled file type patterns that exclude matching file names from the scan results.</summary>
    private readonly FileTypePattern[] _excludedFileTypes;

    /// <summary>Stores the compiled inclusion patterns used to identify the most specific permitted file type match for a file name.</summary>
    private readonly FileTypePattern[] _includedFileTypes;

    /// <summary>Initialises a new instance; the patterns are compiled once.</summary>
    /// <param name="includedFileTypes">The normalised File types entries.</param>
    /// <param name="excludedFileTypes">The normalised Exclude entries.</param>
    public FileTypeFilter(IReadOnlyList<string> includedFileTypes, IReadOnlyList<string> excludedFileTypes)
    {
        // Most specific first, so the first match is the answer: most parts, then fewest wildcard characters, then alphabetical.
        _includedFileTypes =
        [
            .. includedFileTypes.Select(entry => new FileTypePattern(entry))
                                .OrderByDescending(pattern => pattern.PartCount)
                                .ThenBy(pattern => pattern.WildcardCount)
                                .ThenBy(pattern => pattern.Text, StringComparer.Ordinal)
        ];

        _excludedFileTypes = [.. excludedFileTypes.Select(entry => new FileTypePattern(entry))];
    }

    /// <summary>Finds the whitelist entry a file name belongs to. Safe to call concurrently.</summary>
    /// <param name="fileName">The file name, without a folder.</param>
    /// <returns>The most specific matching File types entry, or <see langword="null" /> when an Exclude entry matches or no File types entry does.</returns>
    public FileTypePattern? Match(string fileName)
    {
        /*
         * The following loops are loops instead of LINQ because this is the hottest path in the walk, and the performance is critical. Whilst both versions have the same
         * behaviour, the performance is different. Both LINQ lambdas capture `fileName`, so each call allocates one closure object plus two delegates. The loop version allocates
         * nothing. On .NET 10, `Any()` and `FirstOrDefault()` over an array avoid allocating an enumerator, so the cloure and delegates are the whole csst. `Match` runs once for
         * every file the Unindexed audit finds, so a scan of millions of files makes millions of short-lived allocations. Whilst that isn’t a correctness problem, and the GC
         * handles short-lived objects well, I am still opting for the loop version given how hot this path is.
         */

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (FileTypePattern pattern in _excludedFileTypes)
        {
            if (pattern.Matches(fileName))
            {
                return null;
            }
        }

        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (FileTypePattern pattern in _includedFileTypes)
        {
            if (pattern.Matches(fileName))
            {
                return pattern;
            }
        }

        return null;
    }
}
