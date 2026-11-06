namespace FileAuditor.Services.Checking;

/// <summary>
///     Classifies index paths as found, missing or unverified (SDD §6.3). Thread-safe; use one instance per run, because it caches what it learns about roots and
///     directories.
/// </summary>
/// <param name="reportEmptyFiles">Whether a zero-length file is reported as <see cref="CheckOutcome.MissingEmpty" />.</param>
/// <remarks>
///     <c>File.Exists</c> and <c>Directory.Exists</c> are never used: they return <see langword="false" /> for every failure, which would report files that could not be
///     checked as missing.
/// </remarks>
public sealed class FileChecker(bool reportEmptyFiles)
{
    private static readonly char[] InvalidPathChars = [.. Path.GetInvalidPathChars(), '*', '?'];

    // A null value means the root is reachable; otherwise the outcome shared by every file under it. Lazy keeps a dead host to a single probe.
    private readonly ConcurrentDictionary<string, Lazy<MappedOutcome?>> _roots = new(StringComparer.OrdinalIgnoreCase);

    // Directories found unreachable, keyed with a trailing separator, with the outcome shared by every file under them.
    private readonly ConcurrentDictionary<string, MappedOutcome> _blockedDirectories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Checks one row.
    /// </summary>
    /// <param name="row">The row to check.</param>
    /// <returns>The row's outcome. A failure to check is never reported as Missing.</returns>
    public CheckResult Check(IndexRow row)
    {
        try
        {
            return CheckCore(row);
        }
        catch (Exception ex)
        {
            return ToResult(row, Win32ErrorMapper.Map(ex));
        }
    }

    private CheckResult CheckCore(IndexRow row)
    {
        string path = row.Path;

        if (string.IsNullOrWhiteSpace(path))
            return ToResult(row, new MappedOutcome(CheckOutcome.UnverifiedPathEmpty));

        // Fully qualified, not merely rooted: "\x.txt" and "C:x.txt" would otherwise be resolved against the current directory.
        if (!Path.IsPathFullyQualified(path) || path.AsSpan().IndexOfAny(InvalidPathChars) >= 0)
            return ToResult(row, new MappedOutcome(CheckOutcome.UnverifiedInvalidCharacters));

        string root = AsDirectoryKey(Path.GetPathRoot(path) ?? string.Empty);
        MappedOutcome? rootFailure = _roots.GetOrAdd(root, key => new Lazy<MappedOutcome?>(() => ProbeRoot(key))).Value;

        if (rootFailure is { } failedRoot)
            return ToResult(row, failedRoot);

        List<string> ancestors = GetAncestors(path, root);

        foreach (string directory in ancestors)
        {
            if (_blockedDirectories.TryGetValue(directory, out MappedOutcome blocked))
                return ToResult(row, blocked);
        }

        MappedOutcome own;

        try
        {
            FileAttributes attributes = File.GetAttributes(path);

            if ((attributes & FileAttributes.Directory) != 0)
                return ToResult(row, new MappedOutcome(CheckOutcome.UnverifiedOther, "Path is a directory", "The path refers to a directory, not a file."));

            if (reportEmptyFiles && new FileInfo(path).Length == 0)
                return ToResult(row, new MappedOutcome(CheckOutcome.MissingEmpty));

            return ToResult(row, new MappedOutcome(CheckOutcome.Found));
        }
        catch (Exception ex)
        {
            own = Win32ErrorMapper.Map(ex);
        }

        if (own.Outcome is CheckOutcome.UnverifiedPermissionDenied or CheckOutcome.UnverifiedNetworkUnreachable or CheckOutcome.UnverifiedOther)
            return LocaliseFailure(row, own, ancestors);

        return ToResult(row, own);
    }

    /// <summary>
    ///     Probes a root once. Returns <see langword="null" /> when it is reachable.
    /// </summary>
    private static MappedOutcome? ProbeRoot(string root)
    {
        try
        {
            File.GetAttributes(root);

            return null;
        }
        catch (Exception ex)
        {
            MappedOutcome mapped = Win32ErrorMapper.Map(ex);

            return mapped.Outcome is CheckOutcome.MissingDoesNotExist or CheckOutcome.UnverifiedDriveOrShareNotFound
                ? new MappedOutcome(CheckOutcome.UnverifiedDriveOrShareNotFound)
                : mapped;
        }
    }

    /// <summary>
    ///     Walks from the root's child down to the file's parent to find the first directory that cannot be reached, and caches it.
    /// </summary>
    private CheckResult LocaliseFailure(IndexRow row, MappedOutcome own, List<string> ancestorsDeepestFirst)
    {
        for (int i = ancestorsDeepestFirst.Count - 1; i >= 0; i--)
        {
            string directory = ancestorsDeepestFirst[i];

            try
            {
                File.GetAttributes(directory);
            }
            catch (Exception ex)
            {
                MappedOutcome mapped = Win32ErrorMapper.Map(ex);

                if (mapped.Outcome is CheckOutcome.UnverifiedPermissionDenied or CheckOutcome.UnverifiedNetworkUnreachable or CheckOutcome.UnverifiedOther)
                {
                    _blockedDirectories.TryAdd(directory, mapped);

                    return ToResult(row, mapped);
                }

                if (mapped.Outcome == CheckOutcome.MissingDoesNotExist)
                    return ToResult(row, mapped);
            }
        }

        // Every directory above the file is reachable, so the failure belongs to the file itself and nothing is cached.
        return ToResult(row, own);
    }

    /// <summary>
    ///     Lists the file's ancestor directories, deepest first, stopping before the root. Keys carry a trailing separator.
    /// </summary>
    private static List<string> GetAncestors(string path, string root)
    {
        List<string> ancestors = [];
        string? directory = Path.GetDirectoryName(path);

        while (directory is not null)
        {
            string key = AsDirectoryKey(directory);

            if (key.Length <= root.Length)
                break;

            ancestors.Add(key);
            directory = Path.GetDirectoryName(directory);
        }

        return ancestors;
    }

    private static string AsDirectoryKey(string directory) =>
        directory.Length > 0 && (directory[^1] is '\\' or '/') ? directory : directory + '\\';

    private static CheckResult ToResult(IndexRow row, MappedOutcome mapped) => new(row, mapped.Outcome, mapped.Reason, mapped.SystemMessage);
}
