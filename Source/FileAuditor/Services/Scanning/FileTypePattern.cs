namespace FileAuditor.Services.Scanning;

/// <summary>One normalised file type entry, such as <c>.pdf</c>, <c>.tar.gz</c> or <c>.zip.0??</c>, compiled for matching file names (milestone 4 spec §3.3).</summary>
public sealed class FileTypePattern
{
    private readonly string[] _parts;

    /// <summary>Initialises a new instance.</summary>
    /// <param name="entry">A normalised entry: lower case with one leading dot, as <see cref="FileTypeList.Parse" /> returns it.</param>
    public FileTypePattern(string entry)
    {
        Text   = entry;
        _parts = entry.TrimStart('.').Split('.');

        WildcardCount  = entry.Count(c => c is '*' or '?');
        IsWildcardOnly = _parts.All(part => part.All(c => c is '*' or '?'));
    }

    /// <summary>Gets the normalised entry.</summary>
    public string Text { get; }

    /// <summary>Gets the number of dot-separated parts.</summary>
    public int PartCount => _parts.Length;

    /// <summary>Gets the number of <c>*</c> and <c>?</c> characters.</summary>
    public int WildcardCount { get; }

    /// <summary>Gets a value indicating whether every part is only <c>*</c> and <c>?</c>.</summary>
    public bool IsWildcardOnly { get; }

    /// <summary>Determines whether a file name matches: the entry's parts equal the name's last parts, after a non-empty stem, ignoring case.</summary>
    /// <param name="fileName">The file name, without a folder.</param>
    /// <returns><see langword="true" /> when it matches.</returns>
    public bool Matches(string fileName)
    {
        ReadOnlySpan<char> name = fileName;

        int firstDot = name.IndexOf('.');

        // No dot, or a leading dot: no stem, so no extension.
        if (firstDot <= 0)
        {
            return false;
        }

        int end = name.Length;

        for (int i = _parts.Length - 1; i >= 0; i--)
        {
            int dot = name[..end].LastIndexOf('.');

            if (dot < firstDot)
            {
                return false;
            }

            ReadOnlySpan<char> part = name[(dot + 1)..end];

            if (part.Length == 0
                || !GlobMatches(_parts[i], part))
            {
                return false;
            }

            end = dot;
        }

        return true;
    }

    /// <summary>Gets the part of a matching file name this entry matched: its last <see cref="PartCount" /> parts, in lower case, with a leading dot.</summary>
    /// <param name="fileName">A file name that <see cref="Matches" /> accepted.</param>
    /// <returns>The found extension, such as <c>.zip.002</c>.</returns>
    public string FoundExtension(string fileName)
    {
        int start = fileName.Length;

        for (int i = 0; i < _parts.Length; i++)
        {
            start = fileName.LastIndexOf('.', start - 1);
        }

        return fileName[start..].ToLowerInvariant();
    }

    // Glob match of one part: '*' is any run (including none), '?' exactly one character; case-insensitive. The pattern is already lower case.
    private static bool GlobMatches(string pattern, ReadOnlySpan<char> text)
    {
        int p        = 0;
        int t        = 0;
        int star     = -1;
        int starText = 0;

        while (t < text.Length)
        {
            if (p < pattern.Length
                && pattern[p] == '*')
            {
                star     = p++;
                starText = t;
            }
            else if (p < pattern.Length
                     && (pattern[p] == '?' || pattern[p] == char.ToLowerInvariant(text[t])))
            {
                p++;
                t++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                t = ++starText;
            }
            else
            {
                return false;
            }
        }

        while (p < pattern.Length
               && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }
}
