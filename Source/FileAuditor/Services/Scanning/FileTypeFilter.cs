namespace FileAuditor.Services.Scanning;

/// <summary>Decides which file type entry, if any, a file name belongs to: the blacklist wins, and overlaps go to the most specific whitelist entry.</summary>
public sealed class FileTypeFilter
{
    private readonly FileTypePattern[] _include;

    private readonly FileTypePattern[] _exclude;

    /// <summary>Initialises a new instance; the patterns are compiled once.</summary>
    /// <param name="include">The normalised File types entries.</param>
    /// <param name="exclude">The normalised Exclude entries.</param>
    public FileTypeFilter(IReadOnlyList<string> include, IReadOnlyList<string> exclude)
    {
        // Most specific first, so the first match is the answer: most parts, then fewest wildcard characters, then alphabetical.
        _include = include.Select(entry => new FileTypePattern(entry))
                          .OrderByDescending(pattern => pattern.PartCount)
                          .ThenBy(pattern => pattern.WildcardCount)
                          .ThenBy(pattern => pattern.Text, StringComparer.Ordinal)
                          .ToArray();
        _exclude = exclude.Select(entry => new FileTypePattern(entry))
                          .ToArray();
    }

    /// <summary>Finds the whitelist entry a file name belongs to. Safe to call concurrently.</summary>
    /// <param name="fileName">The file name, without a folder.</param>
    /// <returns>The most specific matching File types entry, or <see langword="null" /> when an Exclude entry matches or no File types entry does.</returns>
    public FileTypePattern? Match(string fileName)
    {
        foreach (FileTypePattern pattern in _exclude)
        {
            if (pattern.Matches(fileName))
            {
                return null;
            }
        }

        foreach (FileTypePattern pattern in _include)
        {
            if (pattern.Matches(fileName))
            {
                return pattern;
            }
        }

        return null;
    }
}
