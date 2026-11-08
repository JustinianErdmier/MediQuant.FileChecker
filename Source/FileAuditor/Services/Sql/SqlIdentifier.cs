namespace FileAuditor.Services.Sql;

/// <summary>Quotes and parses SQL Server identifiers so that user-entered names are never concatenated into SQL raw.</summary>
public static class SqlIdentifier
{
    /// <summary>Wraps an identifier part in square brackets, doubling any closing bracket.</summary>
    /// <param name="part">The unquoted identifier part.</param>
    /// <returns>The bracket-quoted part.</returns>
    public static string Quote(string part) => $"[{part.Replace(oldValue: "]", newValue: "]]")}]";

    /// <summary>Splits a dotted, optionally bracketed name such as <c>dbo.My Table</c> or <c>[dbo].[Odd]]Name]</c> into its unquoted parts.</summary>
    /// <param name="text">The text entered by the user.</param>
    /// <param name="maxParts">The most parts the name may have.</param>
    /// <returns>The unquoted parts, or <c>Query.InvalidIdentifier</c> when the text is empty, malformed or has too many parts.</returns>
    public static ErrorOr<IReadOnlyList<string>> ParseMultipart(string text, int maxParts)
    {
        string trimmed = text.Trim();

        if (trimmed is [])
        {
            return QueryErrors.InvalidIdentifier(text);
        }

        List<string> parts = [];

        int index = 0;

        while (true)
        {
            while (index < trimmed.Length
                   && char.IsWhiteSpace(trimmed[index]))
            {
                index++;
            }

            StringBuilder part        = new();
            bool          isBracketed = index < trimmed.Length && trimmed[index] is '[';

            if (isBracketed)
            {
                index++;

                bool closed = false;

                while (index < trimmed.Length)
                {
                    char character = trimmed[index];

                    if (character is ']')
                    {
                        if (index + 1 < trimmed.Length
                            && trimmed[index + 1] is ']')
                        {
                            part.Append(value: ']');

                            index += 2;

                            continue;
                        }

                        closed = true;

                        index++;

                        break;
                    }

                    part.Append(character);

                    index++;
                }

                if (!closed)
                {
                    return QueryErrors.InvalidIdentifier(text);
                }

                while (index < trimmed.Length
                       && char.IsWhiteSpace(trimmed[index]))
                {
                    index++;
                }

                if (index < trimmed.Length
                    && trimmed[index] is not '.')
                {
                    return QueryErrors.InvalidIdentifier(text);
                }
            }
            else
            {
                while (index < trimmed.Length
                       && trimmed[index] is not '.')
                {
                    if (trimmed[index] is '[' or ']')
                    {
                        return QueryErrors.InvalidIdentifier(text);
                    }

                    part.Append(trimmed[index]);

                    index++;
                }
            }

            string value = isBracketed
                               ? part.ToString()
                               : part.ToString()
                                     .Trim();

            if (value is [])
            {
                return QueryErrors.InvalidIdentifier(text);
            }

            parts.Add(value);

            if (index >= trimmed.Length)
            {
                break;
            }

            index++; // Skip the dot; a trailing dot yields an empty final part and is rejected above.

            if (index >= trimmed.Length)
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
