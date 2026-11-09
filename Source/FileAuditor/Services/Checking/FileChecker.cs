namespace FileAuditor.Services.Checking;

/// <summary>Classifies index paths as found, missing, or unverified (SDD §6.3). Thread-safe; use one instance per run, because it caches what it learns about roots and directories.</summary>
/// <param name="reportEmptyFiles">Whether a zero-length file is reported as <see cref="CheckOutcomes.MissingEmpty" />.</param>
/// <remarks>
///     <c>File.Exists</c> and <c>Directory.Exists</c> are never used: they return <see langword="false" /> for every failure, which would report files that could not be checked
///     as missing.
/// </remarks>
public sealed class FileChecker(bool reportEmptyFiles)
{
    private const int MaxSegmentLength = 255;

    private const long RootReprobeIntervalMilliseconds = 5_000;

    private static readonly char[] InvalidPathChars = [.. Path.GetInvalidPathChars(), '*', '?'];

    private static readonly char[] Separators = ['\\', '/'];

    // Directories found unreachable, keyed with a trailing separator, with the outcome shared by every file under them.
    private readonly ConcurrentDictionary<string, MappedOutcome> _blockedDirectories = new(StringComparer.OrdinalIgnoreCase);

    // ReSharper disable once GrammarMistakeInComment
    // When each reachable root was last confirmed reachable, in Environment.TickCount64 milliseconds. A not-found outcome only re-probes its root when this is stale,
    // so a healthy root with many missing files is not probed once per file.
    private readonly ConcurrentDictionary<string, long> _rootConfirmedAt = new(StringComparer.OrdinalIgnoreCase);

    // A null value means the root is reachable; otherwise the outcome shared by every file under it. Lazy keeps a dead host to a single probe.
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
    ///     Re-probes a root that was cached as reachable after a not-found outcome, at most once per interval. When the root is gone the cached entry is flipped so later files under
    ///     it are not probed again.
    /// </summary>
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

    private static string AsDirectoryKey(string directory) => directory.Length > 0 && directory[^1] is '\\' or '/' ? directory : directory + '\\';

    private static CheckResult ToResult(IndexRow row, MappedOutcome mapped) => new(row, mapped.Outcome, mapped.Reason, mapped.SystemMessage);
}
