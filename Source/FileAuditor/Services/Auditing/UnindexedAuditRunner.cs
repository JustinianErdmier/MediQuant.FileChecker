namespace FileAuditor.Services.Auditing;

/// <summary>
///     Runs one unindexed-files audit: loads the index paths as hashes, walks the folders checking each file as it is found, then writes the report (milestone 4
///     spec §3.5).
/// </summary>
public sealed class UnindexedAuditRunner
{
    private readonly IndexTableReader _reader = new();

    private readonly FolderWalker _walker = new();

    /// <summary>Runs the audit.</summary>
    /// <param name="request">The settings and report path.</param>
    /// <param name="progress">Receives the live statistics.</param>
    /// <param name="ct">Stops the run; once the walk has started, whatever was found is reported.</param>
    /// <returns>
    ///     The report that was written for a completed or stopped audit; a SQL error when loading the index failed; <see cref="AuditErrors.StoppedBeforeChecking" /> when stopped
    ///     while the index was loading; or <c>Report.Unwritable</c> when the report could not be written.
    /// </returns>
    public async Task<ErrorOr<UnindexedReport>> RunAsync(AuditRequest request, UnindexedProgress progress, CancellationToken ct)
    {
        AppSettings settings   = request.Settings;
        DateTime    startedUtc = DateTime.UtcNow;

        // Each folder is made canonical once, and the same list goes to the index query and to the walk, so both see the same prefixes whatever the user typed
        // (`U:/A/./B`, a trailing separator, other letter case). The report still names each folder as it was entered.
        ScanFolder[] folders = (settings.ScanFolders ?? []).Select(folder => folder with { Path = FolderWalker.NormaliseFolder(folder.Path) })
                                                           .ToArray();

        Dictionary<string, string> entered = new(StringComparer.OrdinalIgnoreCase);

        foreach (ScanFolder folder in settings.ScanFolders ?? [])
        {
            entered.TryAdd(FolderWalker.NormaliseFolder(folder.Path), folder.Path);
        }

        ErrorOr<PathQuery> built = QueryBuilder.BuildPaths(settings.Table, settings.PathColumn, folders.Select(folder => folder.Path).ToArray());

        if (built.IsError)
        {
            return built.Errors;
        }

        progress.Phase = "Loading the index...";

        IndexPathSet     paths = new();
        ErrorOr<Success> loaded;

        try
        {
            loaded = await _reader.ReadPathsAsync(ConnectionStrings.Build(settings.Server, settings.Database), built.Value, paths.Add, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return AuditErrors.StoppedBeforeChecking;
        }

        if (loaded.IsError)
        {
            return loaded.Errors;
        }

        if (ct.IsCancellationRequested)
        {
            return AuditErrors.StoppedBeforeChecking;
        }

        progress.Phase = "Checking files...";

        FileTypeFilter filter = new(settings.FileTypes ?? FileTypeList.Defaults, settings.ExcludeTypes ?? []);

        ConcurrentBag<ScannedFile> unindexed =
            [];

        ConcurrentBag<UnverifiedFolder> unverified =
            [];

        // Files reached through a link that leads outside every input: the index may hold their target path, which the prefix filter did not load.
        ConcurrentBag<ScannedFile> deferred =
            [];

        // The walker returns normally when stopped, so a stop is told apart by the token afterwards. An exception from a callback is a bug and surfaces as one.
        await _walker.WalkAsync(folders,
                                filter,
                                walked =>
                                {
                                    ScannedFile file = walked with { InputFolder = entered.GetValueOrDefault(walked.InputFolder, walked.InputFolder) };

                                    if (paths.Contains(file.Path)
                                        || (file.CanonicalPath is not null && paths.Contains(file.CanonicalPath)))
                                    {
                                        progress.Record(indexed: true);
                                    }
                                    else if (file.CanonicalPath is null)
                                    {
                                        unindexed.Add(file);
                                        progress.Record(indexed: false);
                                    }
                                    else
                                    {
                                        deferred.Add(file);
                                    }
                                },
                                folder =>
                                {
                                    unverified.Add(folder);
                                    progress.RecordUnverified();
                                },
                                ct);

        long    notChecked     = 0;
        string? failureMessage = null;

        if (!deferred.IsEmpty)
        {
            // A failure here keeps everything already decided: the report is written as Failed, and the held-back files are in neither count.
            (notChecked, failureMessage) = ct.IsCancellationRequested
                                               ? (deferred.Count, null)
                                               : await ResolveDeferredAsync(settings, built.Value, paths, deferred, unindexed, progress, ct);
        }

        AuditResultKind kind = failureMessage is not null ? AuditResultKind.Failed
                               : ct.IsCancellationRequested ? AuditResultKind.StoppedByUser
                               : AuditResultKind.Completed;

        progress.Phase = "Writing report...";

        UnindexedReport report = new(settings,
                                     startedUtc,
                                     DateTime.UtcNow,
                                     kind,
                                     unindexed.ToArray(),
                                     unverified.ToArray(),
                                     progress.TotalFiles,
                                     progress.Indexed,
                                     notChecked,
                                     failureMessage);

        ErrorOr<Success> written = ReportWriter.Write(request.ReportPath.FullPath, UnindexedReportBuilder.Build(report));

        if (written.IsError)
        {
            return written.Errors;
        }

        return report;
    }

    // Decides the files that were reached through a link outside every input: loads the index rows under the link targets (all the rows were loaded already when
    // the first query had no filter), then counts each file as indexed when the index holds its target path. A stop while the rows load abandons these files:
    // they were not checked, so they are in neither count. A SQL failure does the same and yields its message. Returns how many files were not checked and the
    // failure message, if any.
    private async Task<(long NotChecked, string? FailureMessage)> ResolveDeferredAsync(AppSettings settings, PathQuery first, IndexPathSet paths,
                                                                                       ConcurrentBag<ScannedFile> deferred, ConcurrentBag<ScannedFile> unindexed,
                                                                                       UnindexedProgress progress, CancellationToken ct)
    {
        IndexPathSet targets = paths;

        if (first.Parameters.Count > 0)
        {
            ErrorOr<PathQuery> built = QueryBuilder.BuildPaths(settings.Table, settings.PathColumn, TargetRoots(deferred));

            if (built.IsError)
            {
                return (deferred.Count, built.FirstError.Description);
            }

            progress.Phase = "Loading the index...";

            targets = new IndexPathSet();

            ErrorOr<Success> loaded;

            try
            {
                loaded = await _reader.ReadPathsAsync(ConnectionStrings.Build(settings.Server, settings.Database), built.Value, targets.Add, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return (deferred.Count, null);
            }

            if (loaded.IsError)
            {
                return (deferred.Count, loaded.FirstError.Description);
            }

            if (ct.IsCancellationRequested)
            {
                return (deferred.Count, null);
            }

            progress.Phase = "Checking files...";
        }

        foreach (ScannedFile file in deferred)
        {
            bool indexed = targets.Contains(file.CanonicalPath!);

            if (!indexed)
            {
                unindexed.Add(file);
            }

            progress.Record(indexed);
        }

        return (0, null);
    }

    // The fewest folders whose prefixes cover every canonical path (a folder under another one is left out); once there are more than the prefix filter takes,
    // the list is long enough for the query to load every row instead.
    private static List<string> TargetRoots(ConcurrentBag<ScannedFile> deferred)
    {
        List<string> roots = [];

        foreach (string folder in deferred.Select(file => FolderWalker.NormaliseFolder(Path.GetDirectoryName(file.CanonicalPath)!))
                                          .Distinct(StringComparer.OrdinalIgnoreCase)
                                          .OrderBy(folder => folder.Length))
        {
            if (roots.Any(root => folder.StartsWith(root, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            roots.Add(folder);

            if (roots.Count > QueryBuilder.MaxFilterFolders)
            {
                break;
            }
        }

        return roots;
    }
}
