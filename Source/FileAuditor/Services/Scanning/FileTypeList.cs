namespace FileAuditor.Services.Scanning;

/// <summary>Parses, normalises and formats the File types and Exclude lists (milestone 4 spec §3.3).</summary>
public static class FileTypeList
{
    private static readonly char[] Forbidden = Path.GetInvalidFileNameChars()
                                                   .Where(c => c is not ('*' or '?'))
                                                   .ToArray();

    /// <summary>Gets the built-in File types, sorted.</summary>
    public static IReadOnlyList<string> Defaults { get; } =
    [
        ".bmp",
        ".doc",
        ".docx",
        ".eml",
        ".gif",
        ".jpeg",
        ".jpg",
        ".msg",
        ".odt",
        ".pdf",
        ".png",
        ".ppt",
        ".pptx",
        ".rtf",
        ".tif",
        ".tiff",
        ".xls",
        ".xlsx",
        ".xps"
    ];

    /// <summary>Parses the text of a file types box: entries separated by white space or commas.</summary>
    /// <param name="text">The text typed.</param>
    /// <returns>
    ///     The normalised entries (lower case, one leading dot, no duplicates, sorted), or a validation error naming the first invalid entry as typed. Empty text is an
    ///     empty list.
    /// </returns>
    public static ErrorOr<string[]> Parse(string text)
    {
        SortedSet<string> entries = new(StringComparer.Ordinal);

        foreach (string raw in text.Split([ ',', ' ', '\t', '\r', '\n' ], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string? normalised = Normalise(raw);

            if (normalised is null)
            {
                return Error.Validation(code: "FileTypes.Invalid", $"This file type is not valid:\n{raw}");
            }

            entries.Add(normalised);
        }

        return entries.ToArray();
    }

    /// <summary>Formats entries for a file types box: sorted and space-separated.</summary>
    /// <param name="types">The normalised entries.</param>
    /// <returns>The text.</returns>
    public static string Format(IEnumerable<string> types) => string.Join(separator: ' ', types.OrderBy(type => type, StringComparer.Ordinal));

    private static string? Normalise(string raw)
    {
        string entry = raw.ToLowerInvariant();

        if (entry.StartsWith(value: "*.", StringComparison.Ordinal))
        {
            entry = entry[2..];
        }
        else if (entry.StartsWith(value: '.'))
        {
            entry = entry[1..];
        }

        if (entry.Length == 0
            || entry.IndexOfAny(Forbidden) >= 0
            || entry.Split('.').Any(part => part.Length == 0))
        {
            return null;
        }

        return "." + entry;
    }
}
