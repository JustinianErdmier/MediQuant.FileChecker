namespace FileAuditor.Common.Text;

/// <summary>
///     Prepares file-system paths for display in wrapping text.
/// </summary>
public static class PathText
{
    private const char ZeroWidthSpace = '​';

    private const char WordJoiner = '⁠';

    private const char NoBreakSpace = ' ';

    /// <summary>
    ///     Returns the path with line breaks allowed only after a <c>\</c> or <c>/</c> separator.
    /// </summary>
    /// <remarks>
    ///     A wrapping text block would otherwise break a path at spaces and hyphens, splitting a folder name across lines. A zero-width space follows each
    ///     separator, each space becomes a no-break space and each hyphen is followed by a word joiner. The result is for display only; never use it as a path.
    /// </remarks>
    /// <param name="path">The path to display.</param>
    /// <returns>The path, ready to place in wrapping text.</returns>
    public static string Breakable(string path)
    {
        StringBuilder builder = new(path.Length + path.Length / 8);

        foreach (char c in path)
        {
            switch (c)
            {
                case '\\' or '/':
                    builder.Append(c).Append(ZeroWidthSpace);

                    break;

                case ' ':
                    builder.Append(NoBreakSpace);

                    break;

                case '-':
                    builder.Append(c).Append(WordJoiner);

                    break;

                default:
                    builder.Append(c);

                    break;
            }
        }

        return builder.ToString();
    }
}
