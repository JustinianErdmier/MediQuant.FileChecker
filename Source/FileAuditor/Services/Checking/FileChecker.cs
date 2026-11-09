namespace FileAuditor.Services.Checking;

/// <summary>Classifies index paths as found, missing, or unverified. Thread-safe; use one instance per run, because it caches what it learns about roots and directories.</summary>
/// <param name="reportEmptyFiles">Whether a zero-length file is reported as <see cref="CheckOutcomes.MissingEmpty" />.</param>
/// <remarks>
///     <c>File.Exists</c> and <c>Directory.Exists</c> are never used: they return <see langword="false" /> for every failure, which would report files that could not be checked
///     as missing.
/// </remarks>
public sealed class FileChecker(bool reportEmptyFiles)
{
    /// <summary>The longest a single path component may be, in characters.</summary>
    private const int MaxSegmentLength = 255;

    /// <summary>How long a root stays trusted as reachable after it was last confirmed, in milliseconds.</summary>
    private const long RootReprobeIntervalMilliseconds = 5_000;

    /// <summary>The characters that make a path invalid: those the system rejects, plus the wildcards <c>*</c> and <c>?</c>.</summary>
    private static readonly char[] InvalidPathChars = [.. Path.GetInvalidPathChars(), '*', '?'];

    /// <summary>The path separators, both of which Windows accepts.</summary>
    private static readonly char[] Separators = ['\\', '/'];

    /// <summary>The directories found unreachable, keyed with a trailing separator, with the outcome shared by every file under them.</summary>
    private readonly ConcurrentDictionary<string, MappedOutcome> _blockedDirectories = new(StringComparer.OrdinalIgnoreCase);

    // ReSharper disable once GrammarMistakeInComment
    /// <summary>
    ///     When each reachable root was last confirmed reachable, in <see cref="Environment.TickCount64" /> milliseconds. A not-found outcome only re-probes its root when this is
    ///     stale, so a healthy root with many missing files is not probed once per file.
    /// </summary>
    private readonly ConcurrentDictionary<string, long> _rootConfirmedAt = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     The outcome of probing each root: <see langword="null" /> when the root is reachable, otherwise the outcome shared by every file under it. Lazy keeps a dead host to a
    ///     single probe.
    /// </summary>
    private readonly ConcurrentDictionary<string, Lazy<MappedOutcome?>> _roots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Checks one row.</summary>
    /// <param name="row">The row to check.</param>
    /// <returns>The row's outcome. A failure to check is never reported as Missing.</returns>
    public CheckResult Check(IndexRow row)
    {
        try
        {
            return CheckCore(row);
        }
        catch (Exception exception)
        {
            return ToResult(row, Win32ErrorMapper.Map(exception));
        }
    }

    /// <summary>Classifies one row. <see cref="Check" /> wraps this so that no exception escapes.</summary>
    /// <param name="row">The row to check.</param>
    /// <returns>The row's outcome.</returns>
    private CheckResult CheckCore(IndexRow row)
    {
        string path = row.Path;

        if (SkipMarker.IsSkipped(path))
        {
            return ToResult(row, new MappedOutcome(CheckOutcomes.Skipped));
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return ToResult(row, new MappedOutcome(CheckOutcomes.UnverifiedPathEmpty));
        }

        // Fully qualified, not merely rooted: "\x.txt" and "C:x.txt" would otherwise be resolved against the current directory.
        if (!Path.IsPathFullyQualified(path)
            || path.AsSpan()
                   .IndexOfAny(InvalidPathChars)
            >= 0)
        {
            return ToResult(row, new MappedOutcome(CheckOutcomes.UnverifiedInvalidCharacters));
        }

        // A single path component over the file-system limit cannot be checked; say so rather than report it missing.
        if (path.Split(Separators)
                .Any(segment => segment.Length > MaxSegmentLength))
        {
            return ToResult(row, new MappedOutcome(CheckOutcomes.UnverifiedPathTooLong));
        }

        string root = AsDirectoryKey(Path.GetPathRoot(path) ?? string.Empty);

        MappedOutcome? rootFailure = _roots.GetOrAdd(root, key => new Lazy<MappedOutcome?>(() => ProbeCachedRoot(key)))
                                           .Value;

        if (rootFailure is { } failedRoot)
        {
            return ToResult(row, failedRoot);
        }

        List<string> ancestors = GetAncestors(path, root);

        foreach (string directory in ancestors)
        {
            if (_blockedDirectories.TryGetValue(directory, out MappedOutcome blocked))
            {
                return ToResult(row, blocked);
            }
        }

        MappedOutcome fileOutcome;

        try
        {
            FileAttributes attributes = File.GetAttributes(path);

            if ((attributes & FileAttributes.Directory) is not 0)
            {
                return ToResult(row, new MappedOutcome(CheckOutcomes.UnverifiedOther, Reason: "Path is a directory", SystemMessage: "The path refers to a directory, not a file."));
            }

            if (reportEmptyFiles && new FileInfo(path).Length is 0)
            {
                return ToResult(row, new MappedOutcome(CheckOutcomes.MissingEmpty));
            }

            return ToResult(row, new MappedOutcome(CheckOutcomes.Found));
        }
        catch (Exception exception)
        {
            fileOutcome = Win32ErrorMapper.Map(exception);
        }

        CheckResult result = fileOutcome.Outcome is CheckOutcomes.UnverifiedPermissionDenied or CheckOutcomes.UnverifiedNetworkUnreachable or CheckOutcomes.UnverifiedOther
                                 ? LocaliseFailure(row, fileOutcome, ancestors)
                                 : ToResult(row, fileOutcome);

        // The root was reachable when it was cached, but a share or drive can disappear mid-run; a not-found outcome is only trustworthy if the root is still there.
        if (result.Outcome is CheckOutcomes.MissingDoesNotExist
            && RootHasVanished(root) is { } vanished)
        {
            return ToResult(row, vanished);
        }

        return result;
    }

    /// <summary>
    ///     Re-probes a root that was cached as reachable after a not-found outcome, at most once per interval. When the root is gone the cached entry is flipped so later files
    ///     under it are not probed again.
    /// </summary>
    /// <param name="root">The root, with a trailing separator.</param>
    /// <returns><see langword="null" /> when the root is still reachable; otherwise the outcome shared by every file under it.</returns>
    private MappedOutcome? RootHasVanished(string root)
    {
        long now = Environment.TickCount64;

        if (_rootConfirmedAt.TryGetValue(root, out long confirmedAt)
            && now - confirmedAt < RootReprobeIntervalMilliseconds)
        {
            return null;
        }

        MappedOutcome? failure = ProbeRoot(root);

        if (failure is null)
        {
            _rootConfirmedAt[root] = now;

            return null;
        }

        _roots[root] = new Lazy<MappedOutcome?>(failure);

        return failure;
    }

    /// <summary>Probes a root and, when it is reachable, records when it was confirmed.</summary>
    /// <param name="root">The root, with a trailing separator.</param>
    /// <returns><see langword="null" /> when the root is reachable; otherwise the outcome shared by every file under it.</returns>
    private MappedOutcome? ProbeCachedRoot(string root)
    {
        MappedOutcome? failure = ProbeRoot(root);

        if (failure is null)
        {
            _rootConfirmedAt[root] = Environment.TickCount64;
        }

        return failure;
    }

    /// <summary>Walks from the root's child down to the file's parent to find the first directory that cannot be reached, and caches it.</summary>
    /// <param name="row">The row whose file could not be checked.</param>
    /// <param name="fileOutcome">The outcome the file itself produced, used when every directory above it is reachable.</param>
    /// <param name="ancestorsDeepestFirst">The file's ancestor directories, deepest first.</param>
    /// <returns>The result for the row, naming the first unreachable directory when there is one.</returns>
    private CheckResult LocaliseFailure(IndexRow row, MappedOutcome fileOutcome, List<string> ancestorsDeepestFirst)
    {
        for (int index = ancestorsDeepestFirst.Count - 1; index >= 0; index--)
        {
            string directory = ancestorsDeepestFirst[index];

            try
            {
                File.GetAttributes(directory);
            }
            catch (Exception exception)
            {
                MappedOutcome mapped = Win32ErrorMapper.Map(exception);

                switch (mapped.Outcome)
                {
                    case CheckOutcomes.UnverifiedPermissionDenied or CheckOutcomes.UnverifiedNetworkUnreachable or CheckOutcomes.UnverifiedOther:
                        _blockedDirectories.TryAdd(directory, mapped);

                        return ToResult(row, mapped);

                    case CheckOutcomes.MissingDoesNotExist:
                        return ToResult(row, mapped);

                    // Any other outcome says nothing about this ancestor; the next one down is probed.
                    case CheckOutcomes.Found or
                        CheckOutcomes.Skipped or
                        CheckOutcomes.MissingEmpty or
                        CheckOutcomes.UnverifiedDriveOrShareNotFound or
                        CheckOutcomes.UnverifiedPathTooLong or
                        CheckOutcomes.UnverifiedLinkNotFollowed or
                        CheckOutcomes.UnverifiedInvalidCharacters or
                        CheckOutcomes.UnverifiedPathEmpty:
                        break;

                    default:
                        throw new UnreachableException();
                }
            }
        }

        // Every directory above the file is reachable, so the failure belongs to the file itself and nothing is cached.
        return ToResult(row, fileOutcome);
    }

    /// <summary>Probes a root once. Returns <see langword="null" /> when it is reachable.</summary>
    /// <param name="root">The root, with a trailing separator.</param>
    /// <returns>The outcome shared by every file under the root, or <see langword="null" /> when it is reachable.</returns>
    private static MappedOutcome? ProbeRoot(string root)
    {
        try
        {
            File.GetAttributes(root);

            return null;
        }
        catch (Exception exception)
        {
            MappedOutcome mapped = Win32ErrorMapper.Map(exception);

            return mapped.Outcome is CheckOutcomes.MissingDoesNotExist or CheckOutcomes.UnverifiedDriveOrShareNotFound
                       ? new MappedOutcome(CheckOutcomes.UnverifiedDriveOrShareNotFound)
                       : mapped;
        }
    }

    /// <summary>Lists the file's ancestor directories, deepest first, stopping before the root. Keys carry a trailing separator.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <param name="root">The path's root, with a trailing separator.</param>
    /// <returns>The ancestor directories, deepest first.</returns>
    private static List<string> GetAncestors(string path, string root)
    {
        List<string> ancestors = [];

        string? directory = Path.GetDirectoryName(path);

        while (directory is not null)
        {
            string key = AsDirectoryKey(directory);

            if (key.Length <= root.Length)
            {
                break;
            }

            ancestors.Add(key);
            directory = Path.GetDirectoryName(directory);
        }

        return ancestors;
    }

    /// <summary>Gets the directory as a dictionary key, with a trailing separator.</summary>
    /// <param name="directory">The directory path.</param>
    /// <returns>The path, ending in <c>\</c> or <c>/</c>.</returns>
    private static string AsDirectoryKey(string directory) => directory.Length > 0 && directory[^1] is '\\' or '/' ? directory : directory + '\\';

    /// <summary>Builds the result of a row from the outcome mapped for it.</summary>
    /// <param name="row">The row that was checked.</param>
    /// <param name="mapped">The outcome and its reason and system message.</param>
    /// <returns>The row's result.</returns>
    private static CheckResult ToResult(IndexRow row, MappedOutcome mapped) => new(row, mapped.Outcome, mapped.Reason, mapped.SystemMessage);
}
