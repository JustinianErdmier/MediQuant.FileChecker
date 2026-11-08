namespace FileAuditor.Services.Auditing;

/// <summary>Runs one unindexed-files audit: loads the index paths as hashes, walks the folders checking each file as it is found, then writes the report (milestone 4 spec §3.5).</summary>
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
        AppSettings          settings   = request.Settings;
        DateTime             startedUtc = DateTime.UtcNow;
        IReadOnlyList<ScanFolder> folders = settings.ScanFolders ?? [];

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

        // The walker returns normally when stopped, so a stop is told apart by the token afterwards. An exception from a callback is a bug and surfaces as one.
        await _walker.WalkAsync(folders,
                                filter,
                                file =>
                                {
                                    bool indexed = paths.Contains(file.Path);

                                    if (!indexed)
                                    {
                                        unindexed.Add(file);
                                    }

                                    progress.Record(indexed);
                                },
                                folder =>
                                {
                                    unverified.Add(folder);
                                    progress.RecordUnverified();
                                },
                                ct);

        AuditResultKind kind = ct.IsCancellationRequested ? AuditResultKind.StoppedByUser : AuditResultKind.Completed;

        progress.Phase = "Writing report...";

        UnindexedReport report = new(settings,
                                     startedUtc,
                                     DateTime.UtcNow,
                                     kind,
                                     unindexed.ToArray(),
                                     unverified.ToArray(),
                                     progress.TotalFiles,
                                     progress.Indexed);

        ErrorOr<Success> written = ReportWriter.Write(request.ReportPath.FullPath, UnindexedReportBuilder.Build(report));

        if (written.IsError)
        {
            return written.Errors;
        }

        return report;
    }
}