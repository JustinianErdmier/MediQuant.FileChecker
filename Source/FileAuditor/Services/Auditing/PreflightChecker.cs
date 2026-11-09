namespace FileAuditor.Services.Auditing;

/// <summary>The checks that run when Start is pressed, before the database is touched. Each failure or declined confirmation is explained in a dialogue.</summary>
/// <param name="dialogues">Shows the errors and confirmations.</param>
/// <param name="projectSettingsService">Remembers which warnings were answered Continue this session.</param>
public sealed class PreflightChecker(DialogueService dialogues, ProjectSettingsService projectSettingsService)
{
    /// <summary>
    ///     Runs the checks in order: identifiers, the two reports' custom names, the Unindexed direction's folders and file types, custom file name, custom location form, report
    ///     folder, existing report.
    /// </summary>
    /// <param name="settings">The settings to audit with.</param>
    /// <param name="direction">The audit direction, which decides the report file name.</param>
    /// <returns>The resolved report path when the run may start; <see langword="null" /> when a check failed or the user declined (the user has been told).</returns>
    public async Task<ReportPath?> RunAsync(AppSettings settings, AuditDirections direction)
    {
        ErrorOr<AuditQuery> query = QueryBuilder.Build(settings.Table, settings.PathColumn, settings.IdColumn);

        if (query.IsError)
        {
            await dialogues.ShowErrorAsync(query.FirstError.Description);

            return null;
        }

        switch (settings.UseCustomReportLocation)
        {
            case false when !ReportPathResolver.IsSingleFolderName(settings.ProjectFolder.Trim()):
                await dialogues.ShowErrorAsync("The project folder must be a single folder name, not a path, and must not contain characters that are not allowed:\n"
                                               + settings.ProjectFolder.Trim());

                return null;

            // Both tabs write into the one custom location, so equal names would have the reports overwrite each other.
            case true when string.Equals(settings.MissingCustomFileName.Trim(), settings.UnindexedCustomFileName.Trim(), StringComparison.OrdinalIgnoreCase):
            {
                string name = ReportPathResolver.Resolve(settings, direction)
                                                .FileName;

                await dialogues.ShowErrorAsync($"Both reports would have the same file name:\n{PathText.Breakable(name)}\n\nGive each tab's report its own name.");

                return null;
            }
        }

        if (direction is AuditDirections.Unindexed
            && !await CheckUnindexedInputsAsync(settings))
        {
            return null;
        }

        ReportPath report = ReportPathResolver.Resolve(settings, direction);

        if (report.IsCustom)
        {
            if (report.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                await dialogues.ShowErrorAsync($"The report file name contains characters that are not allowed:\n{report.FileName}");

                return null;
            }

            if (!Path.IsPathFullyQualified(report.Directory))
            {
                await dialogues.ShowErrorAsync("The report location must be a full path, such as C:\\Reports or \\\\server\\share\\Reports:\n"
                                               + PathText.Breakable(report.Directory));

                return null;
            }
        }

        // The report folder may be on a slow or disconnected drive, so none of these probes run on the UI thread.
        if (!await Task.Run(() => Directory.Exists(report.Directory)))
        {
            if (!report.IsCustom)
            {
                await dialogues.ShowErrorAsync($"The report folder does not exist:\n{PathText.Breakable(report.Directory)}");

                return null;
            }

            if (!await dialogues.ConfirmAsync($"The report folder does not exist:\n{PathText.Breakable(report.Directory)}\n\nDo you want to create it?",
                                              confirmText: "_Create Folder"))
            {
                return null;
            }

            string? failure = await Task.Run(() => TryCreateFolder(report.Directory));

            if (failure is not null)
            {
                await dialogues.ShowErrorAsync($"The report folder could not be created:\n{PathText.Breakable(report.Directory)}\n\n{failure}");

                return null;
            }
        }

        // Better to learn now that the folder is read-only than after the whole table has been checked.
        ErrorOr<Success> writable = await Task.Run(() => ProbeWritable(report));

        if (writable.IsError)
        {
            await dialogues.ShowErrorAsync(writable.FirstError.Description);

            return null;
        }

        if (!await Task.Run(() => File.Exists(report.FullPath)))
        {
            return report;
        }

        // An existing read-only report would pass the folder probe but fail at the final move, after the whole table had been checked.
        if (await Task.Run(() => IsReadOnly(report.FullPath)))
        {
            await dialogues.ShowErrorAsync(ReportErrors.ReadOnly(report.FullPath)
                                                       .Description);

            return null;
        }

        if (!await dialogues.ConfirmAsync($"The report already exists:\n{PathText.Breakable(report.FullPath)}\n\nDo you want to overwrite it?", confirmText: "_Overwrite"))
        {
            return null;
        }

        return report;
    }

    /// <summary>Checks the unindexed-files inputs: the folders first, then the two file type lists, then the three type warnings.</summary>
    /// <param name="settings">The settings to audit with.</param>
    /// <returns><see langword="true" /> when the audit may start; <see langword="false" /> when a check failed or the user declined (the user has been told).</returns>
    private async Task<bool> CheckUnindexedInputsAsync(AppSettings settings)
    {
        ScanFolder[] folders = settings.ScanFolders;

        if (folders.Length is 0)
        {
            await dialogues.ShowErrorAsync(message: "Add at least one folder to check.");

            return false;
        }

        // The folders may be on slow or disconnected shares, so they are probed off the UI thread.
        string? folderProblem = await Task.Run(() => FindFolderProblem(folders));

        if (folderProblem is not null)
        {
            await dialogues.ShowErrorAsync(folderProblem);

            return false;
        }

        // The Edit dialogues only accept valid entries, so an invalid one can come only from a project file edited by hand.
        ErrorOr<string[]> include = FileTypeList.Parse(string.Join(separator: ' ', settings.FileTypes));

        ErrorOr<string[]> exclude = FileTypeList.Parse(string.Join(separator: ' ', settings.ExcludeTypes));

        if (include.IsError
            || exclude.IsError)
        {
            await dialogues.ShowErrorAsync((include.IsError ? include : exclude).FirstError.Description);

            return false;
        }

        if (include.Value.Length is 0)
        {
            await dialogues.ShowErrorAsync(message: "Add at least one file type.");

            return false;
        }

        string? excludeEverything = exclude.Value.FirstOrDefault(entry => new FileTypePattern(entry).MatchesEverything);

        if (excludeEverything is null)
        {
            return await ConfirmTypeWarningsAsync(include.Value, exclude.Value);
        }

        await dialogues.ShowErrorAsync($"The Exclude list contains {excludeEverything}, which matches every file, so nothing would be checked. Remove it to run the audit.");

        return false;
    }

    /// <summary>Asks the user to confirm each type-list oddity that has not already been confirmed this session: a type that matches every file, and a type in both lists.</summary>
    /// <param name="include">The normalised File types entries.</param>
    /// <param name="exclude">The normalised Exclude entries.</param>
    /// <returns><see langword="true" /> when every needed confirmation was given; <see langword="false" /> when the user declined one.</returns>
    private async Task<bool> ConfirmTypeWarningsAsync(string[] include, string[] exclude)
    {
        string? matchAllEntry = include.FirstOrDefault(entry => new FileTypePattern(entry).MatchesEverything);

        if (matchAllEntry is not null
            && include.Length > 1
            && !projectSettingsService.RedundantTypesConfirmed)
        {
            if (!await dialogues.ConfirmAsync($"File types contains {matchAllEntry}, which matches every file, so the other file types are redundant.\n\nDo you want to continue?",
                                              confirmText: "_Continue"))
            {
                return false;
            }

            projectSettingsService.RedundantTypesConfirmed = true;
        }

        if (matchAllEntry is not null
            && exclude.Length > 0
            && !projectSettingsService.ExcludedTypesConfirmed)
        {
            if (!await
                     dialogues.ConfirmAsync($"File types contains {matchAllEntry}, which matches every file, so every file will be checked except the excluded types."
                                            + "\n\nDo you want to continue?",
                                            confirmText: "_Continue"))
            {
                return false;
            }

            projectSettingsService.ExcludedTypesConfirmed = true;
        }

        string[] inBothLists = [.. include.Intersect(exclude, StringComparer.Ordinal)];

        if (inBothLists.Length <= 0
            || projectSettingsService.OverlapConfirmed)
        {
            return true;
        }

        string entries = string.Join(separator: ' ', inBothLists);

        if (!await dialogues.ConfirmAsync($"These types are in both File types and Exclude, so they will not be checked:\n{entries}\n\nDo you want to continue?",
                                          confirmText: "_Continue"))
        {
            return false;
        }

        projectSettingsService.OverlapConfirmed = true;

        return true;
    }

    /// <summary>Finds the first problem with the folder list.</summary>
    /// <param name="folders">The folders the unindexed-files audit checks.</param>
    /// <returns>The message to show, or <see langword="null" /> when every folder is a full path that exists and none is listed twice.</returns>
    private static string? FindFolderProblem(IReadOnlyList<ScanFolder> folders)
    {
        foreach (ScanFolder folder in folders)
        {
            if (!Path.IsPathFullyQualified(folder.Path))
            {
                return $"This folder is not a full path:\n{PathText.Breakable(folder.Path)}";
            }

            if (!Directory.Exists(folder.Path))
            {
                return $"This folder does not exist:\n{PathText.Breakable(folder.Path)}";
            }
        }

        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        return (from folder in folders
                where !seen.Add(FolderWalker.NormaliseFolder(folder.Path))
                select $"This folder is listed twice:\n{PathText.Breakable(folder.Path)}").FirstOrDefault();
    }

    /// <summary>Creates and immediately removes the temporary file that <see cref="ReportWriter" /> uses, so the probe leaves nothing behind.</summary>
    /// <param name="report">The report whose folder is probed.</param>
    /// <returns>Success, or the error that says the folder cannot be written to.</returns>
    private static ErrorOr<Success> ProbeWritable(ReportPath report)
    {
        try
        {
            using FileStream probe = new(report.FullPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);

            return Result.Success;
        }
        catch (Exception exception) when (exception is IOException
                                                       or UnauthorizedAccessException
                                                       or ArgumentException
                                                       or NotSupportedException)
        {
            return ReportErrors.FolderNotWritable(report.Directory, exception.Message);
        }
    }

    /// <summary>Determines whether a file is marked read-only.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns><see langword="true" /> when the file is read-only; <see langword="false" /> when it is not or its attributes cannot be read.</returns>
    private static bool IsReadOnly(string path)
    {
        try
        {
            return File.GetAttributes(path)
                       .HasFlag(FileAttributes.ReadOnly);
        }
        catch (Exception exception) when (exception is IOException
                                                       or UnauthorizedAccessException
                                                       or ArgumentException
                                                       or NotSupportedException)
        {
            // Unreadable attributes are not proof of a read-only file; the write operation itself will report any real failure.
            return false;
        }
    }

    /// <summary>Creates a folder, and any missing folders above it.</summary>
    /// <param name="folder">The full path of the folder.</param>
    /// <returns><see langword="null" /> when the folder was created; otherwise what went wrong.</returns>
    private static string? TryCreateFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);

            return null;
        }
        catch (Exception exception) when (exception is IOException
                                                       or UnauthorizedAccessException
                                                       or ArgumentException
                                                       or NotSupportedException)
        {
            return exception.Message;
        }
    }
}
