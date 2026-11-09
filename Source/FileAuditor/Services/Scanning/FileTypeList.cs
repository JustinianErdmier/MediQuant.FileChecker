namespace FileAuditor.Services.Scanning;

/// <summary>Parses, normalises and formats the File types and Exclude lists.</summary>
public static class FileTypeList
{
    /// <summary>The characters no file type may contain: those not allowed in a file name, except the wildcards <c>*</c> and <c>?</c>.</summary>
    private static readonly char[] Forbidden =
    [
        .. Path.GetInvalidFileNameChars()
               .Where(c => c is not ('*' or '?'))
    ];

    /// <summary>
    ///     Gets the built-in File types, sorted ordinally. The sort is applied here, not left to the literal's order, because an untouched project is recognised by comparing
    ///     its settings with the defaults, and the File types box always holds its entries sorted: an unsorted default would make every fresh window look changed.
    /// </summary>
    public static IReadOnlyList<string> Defaults { get; } =
    [
        .. new[] { ".bmp", ".doc", ".docm", ".docx", ".dotm", ".dotx", ".gif", ".html", ".rtf", ".txt", ".jpeg", ".tiff", ".pdf", ".png", ".jpg" }
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>Splits the text of a file types box into its entries: they are separated by white space (any kind) or commas.</summary>
    /// <param name="text">The text typed.</param>
    /// <returns>The entries as typed, without empty ones.</returns>
    public static string[] Split(string text)
        =>
        [
            .. text.Split(separator: ',')
                   .SelectMany(part => part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        ];

    /// <summary>Parses the text of a file types box: entries separated by white space or commas.</summary>
    /// <param name="text">The text typed.</param>
    /// <returns>The normalised entries (lower case, one leading dot, no duplicates, sorted), or a validation error naming the first invalid entry as typed. Empty text is an empty list.</returns>
    public static ErrorOr<string[]> Parse(string text)
    {
        SortedSet<string> entries = new(StringComparer.Ordinal);

        foreach (string typedEntry in Split(text))
        {
            string? normalised = Normalise(typedEntry);

            if (string.IsNullOrWhiteSpace(normalised))
            {
                return InvalidEntry(typedEntry);
            }

            entries.Add(normalised);
        }

        // ReSharper disable once UseCollectionExpression
        return entries.ToArray();
    }

    /// <summary>Creates the validation error that names an entry that is not a valid file type.</summary>
    /// <param name="typedEntry">The entry as typed.</param>
    /// <returns>The error, whose description is the message to show.</returns>
    public static Error InvalidEntry(string typedEntry) => Error.Validation(code: "FileTypes.Invalid", $"This file type is not valid:\n{typedEntry}");

    /// <summary>Formats entries for a file types box: sorted and space-separated.</summary>
    /// <param name="types">The normalised entries.</param>
    /// <returns>The text.</returns>
    public static string Format(IEnumerable<string> types) => string.Join(separator: ' ', types.OrderBy(type => type, StringComparer.Ordinal));

    /// <summary>Normalises one typed entry to lower case with exactly one leading dot, dropping a leading <c>*.</c>.</summary>
    /// <param name="typedEntry">The entry as typed.</param>
    /// <returns>The normalised entry, or <see langword="null" /> when it is empty, has an empty part or contains a forbidden character.</returns>
    private static string? Normalise(string typedEntry)
    {
        string entry = typedEntry.ToLowerInvariant();

        if (entry.StartsWith(value: "*.", StringComparison.Ordinal))
        {
            entry = entry[2..];
        }
        else if (entry.StartsWith(value: '.'))
        {
            entry = entry[1..];
        }

        if (entry is []
            || entry.IndexOfAny(Forbidden) >= 0
            || entry.Split(separator: '.')
                    .Any(part => part is []))
        {
            return null;
        }

        return "." + entry;
    }
}
