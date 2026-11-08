namespace FileAuditor.Services.Scanning;

/// <summary>One normalised file type entry, such as <c>.pdf</c>, <c>.tar.gz</c> or <c>.zip.0??</c>, compiled for matching file names (milestone 4 spec §3.3).</summary>
public sealed class FileTypePattern
{
    /// <summary>Stores the normalised file type pattern as dot-separated extension parts, excluding the leading dot.</summary>
    private readonly string[] _parts;

    /// <summary>Initialises a new instance.</summary>
    /// <param name="entry">A normalised entry: lower case with one leading dot, as <see cref="FileTypeList.Parse" /> returns it.</param>
    public FileTypePattern(string entry)
    {
        Text = entry;

        _parts = entry.TrimStart(trimChar: '.')
                      .Split(separator: '.');

        WildcardCount  = entry.Count(character => character is '*' or '?');
        IsWildcardOnly = _parts.All(part => part.All(character => character is '*' or '?'));

        // One part of wildcards with a '*' and at most one '?': every extension has at least one character, so only then does it accept every extension. '?' alone accepts
        // only one-character extensions, '?*?' only longer ones, and '.*.*' only names with two extensions.
        MatchesEverything = _parts is [var _]
                            && IsWildcardOnly
                            && entry.Contains(value: '*')
                            && entry.Count(character => character is '?') <= 1;
    }

    /// <summary>Gets a value indicating whether every part is only <c>*</c> and <c>?</c>.</summary>
    public bool IsWildcardOnly { get; }

    /// <summary>Gets a value indicating whether the entry accepts every extension: a single part made only of wildcards, with a <c>*</c> and at most one <c>?</c>.</summary>
    public bool MatchesEverything { get; }

    /// <summary>Gets the number of dot-separated parts.</summary>
    public int PartCount => _parts.Length;

    /// <summary>Gets the normalised entry.</summary>
    public string Text { get; }

    /// <summary>Gets the number of <c>*</c> and <c>?</c> characters.</summary>
    public int WildcardCount { get; }

    /// <summary>Determines whether a file name matches: the entry's parts equal the name's last parts, after a non-empty stem, ignoring case.</summary>
    /// <param name="fileName">The file name, without a folder.</param>
    /// <returns><see langword="true" /> when it matches.</returns>
    public bool Matches(string fileName)
    {
        ReadOnlySpan<char> name = fileName;

        int firstDot = name.IndexOf(value: '.');

        // No dot, or a leading dot: no stem, so no extension.
        if (firstDot <= 0)
        {
            return false;
        }

        int end = name.Length;

        for (int partIndex = _parts.Length - 1; partIndex >= 0; partIndex--)
        {
            int dot = name[..end]
                .LastIndexOf(value: '.');

            if (dot < firstDot)
            {
                return false;
            }

            ReadOnlySpan<char> part = name[(dot + 1)..end];

            if (part is []
                || !GlobMatches(_parts[partIndex], part))
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

        for (int partIndex = 0; partIndex < _parts.Length; partIndex++)
        {
            start = fileName.LastIndexOf(value: '.', start - 1);
        }

        return fileName[start..]
            .ToLowerInvariant();
    }

    // Glob match of one part: '*' is any run (including none), '?' exactly one character; case-insensitive. The pattern is already lower case.
    private static bool GlobMatches(string pattern, ReadOnlySpan<char> text)
    {
        int patternIndex     = 0;
        int textIndex        = 0;
        int starPatternIndex = -1;
        int starTextIndex    = 0;

        while (textIndex < text.Length)
        {
            if (patternIndex < pattern.Length
                && pattern[patternIndex] is '*')
            {
                starPatternIndex = patternIndex++;
                starTextIndex    = textIndex;
            }
            else if (patternIndex < pattern.Length
                     && (pattern[patternIndex] is '?' || pattern[patternIndex] == char.ToLowerInvariant(text[textIndex])))
            {
                patternIndex++;
                textIndex++;
            }
            else if (starPatternIndex >= 0)
            {
                patternIndex = starPatternIndex + 1;
                textIndex    = ++starTextIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length
               && pattern[patternIndex] is '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }
}
