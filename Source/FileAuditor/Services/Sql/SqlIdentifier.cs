namespace FileAuditor.Services.Sql;

/// <summary>
///     Quotes and parses SQL Server identifiers so that user-entered names are never concatenated into SQL raw.
/// </summary>
public static class SqlIdentifier
{
    /// <summary>
    ///     Wraps an identifier part in square brackets, doubling any closing bracket.
    /// </summary>
    /// <param name="part">The unquoted identifier part.</param>
    /// <returns>The bracket-quoted part.</returns>
    public static string Quote(string part) =>
        "[" + part.Replace("]", "]]") + "]";

    /// <summary>
    ///     Splits a dotted, optionally bracketed name such as <c>dbo.My Table</c> or <c>[dbo].[Odd]]Name]</c> into its unquoted parts.
    /// </summary>
    /// <param name="text">The text entered by the user.</param>
    /// <param name="maxParts">The most parts the name may have.</param>
    /// <returns>The unquoted parts, or <c>Query.InvalidIdentifier</c> when the text is empty, malformed or has too many parts.</returns>
    public static ErrorOr<IReadOnlyList<string>> ParseMultipart(string text, int maxParts)
    {
        string trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return QueryErrors.InvalidIdentifier(text);
        }

        List<string> parts = [];
        int i = 0;

        while (true)
        {
            while (i < trimmed.Length && char.IsWhiteSpace(trimmed[i]))
            {
                i++;
            }

            StringBuilder part = new();
            bool isBracketed = i < trimmed.Length && trimmed[i] == '[';

            if (isBracketed)
            {
                i++;
                bool closed = false;

                while (i < trimmed.Length)
                {
                    char c = trimmed[i];

                    if (c == ']')
                    {
                        if (i + 1 < trimmed.Length && trimmed[i + 1] == ']')
                        {
                            part.Append(']');
                            i += 2;

                            continue;
                        }

                        closed = true;
                        i++;

                        break;
                    }

                    part.Append(c);
                    i++;
                }

                if (!closed)
                {
                    return QueryErrors.InvalidIdentifier(text);
                }

                while (i < trimmed.Length && char.IsWhiteSpace(trimmed[i]))
                {
                    i++;
                }

                if (i < trimmed.Length && trimmed[i] != '.')
                {
                    return QueryErrors.InvalidIdentifier(text);
                }
            }
            else
            {
                while (i < trimmed.Length && trimmed[i] != '.')
                {
                    if (trimmed[i] is '[' or ']')
                    {
                        return QueryErrors.InvalidIdentifier(text);
                    }

                    part.Append(trimmed[i]);
                    i++;
                }
            }

            string value = isBracketed ? part.ToString() : part.ToString().Trim();

            if (value.Length == 0)
            {
                return QueryErrors.InvalidIdentifier(text);
            }

            parts.Add(value);

            if (i >= trimmed.Length)
            {
                break;
            }

            i++; // Skip the dot; a trailing dot yields an empty final part and is rejected above.

            if (i >= trimmed.Length)
            {
                return QueryErrors.InvalidIdentifier(text);
            }
        }

        if (parts.Count > maxParts)
        {
            return QueryErrors.InvalidIdentifier(text);
        }

        return parts;
    }
}
