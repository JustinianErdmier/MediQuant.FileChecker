namespace FileAuditor.Common.Text;

/// <summary>Prepares file-system paths for display in wrapping text.</summary>
public static class PathText
{
    /// <summary>The no-break space that replaces each space in a path, so a folder name is not split across lines.</summary>
    private const char NoBreakSpace = ' ';

    /// <summary>The word joiner placed after each hyphen, so the text block does not break the line there.</summary>
    private const char WordJoiner = '⁠';

    /// <summary>The zero-width space placed after each path separator, which is where a line may break.</summary>
    private const char ZeroWidthSpace = '​';

    /// <summary>Returns the path with line breaks allowed only after a <c>\</c> or <c>/</c> separator.</summary>
    /// <remarks>
    ///     A wrapping text block would otherwise break a path at spaces and hyphens, splitting a folder name across lines. A zero-width space follows each separator, each space
    ///     becomes a no-break space and each hyphen is followed by a word joiner. The result is for display only; never use it as a path.
    /// </remarks>
    /// <param name="path">The path to display.</param>
    /// <returns>The path, ready to place in wrapping text.</returns>
    public static string Breakable(string path)
    {
        StringBuilder builder = new(path.Length + path.Length / 8);

        foreach (char character in path)
        {
            switch (character)
            {
                case '\\' or '/':
                    builder.Append(character)
                           .Append(ZeroWidthSpace);

                    break;

                case ' ':
                    builder.Append(NoBreakSpace);

                    break;

                case '-':
                    builder.Append(character)
                           .Append(WordJoiner);

                    break;

                default:
                    builder.Append(character);

                    break;
            }
        }

        return builder.ToString();
    }
}
