namespace FileAuditor.Services.Auditing;

/// <summary>The checks that run when Start is pressed, before the database is touched (SDD §6.1). Each failure or declined confirmation is explained in a dialogue.</summary>
/// <param name="dialogues">Shows the errors and confirmations.</param>
/// <param name="project">Remembers which warnings were answered Continue this session.</param>
public sealed class PreflightChecker(DialogueService dialogues, ProjectSettingsService project)
{
    /// <summary>
    ///     Runs the checks in order: identifiers, the two reports' custom names, the Unindexed direction's folders and file types, custom file name, custom location form, report
    ///     folder, existing report.
    /// </summary>
    /// <param name="settings">The settings to audit with.</param>
    /// <param name="direction">The audit direction, which decides the report file name.</param>
    /// <returns>The resolved report path when the run may start; <see langword="null" /> when a check failed or the user declined (the user has been told).</returns>
    public async Task<ReportPath?> RunAsync(AppSettings settings, AuditDirection direction)
    {
        ErrorOr<AuditQuery> query = QueryBuilder.Build(settings.Table, settings.PathColumn, settings.IdColumn);

        if (query.IsError)
        {
            await dialogues.ShowErrorAsync(query.FirstError.Description);

            return null;
        }

        if (!settings.UseCustomReportLocation
            && !ReportPathResolver.IsSingleFolderName(settings.ProjectFolder.Trim()))
        {
            await dialogues.ShowErrorAsync("The project folder must be a single folder name, not a path, and must not contain characters that are not allowed:\n"
                                           + settings.ProjectFolder.Trim());

            return null;
        }

        // Both tabs write into the one custom location, so equal names would have the reports overwrite each other.
        if (settings.UseCustomReportLocation
            && string.Equals(settings.MissingCustomFileName.Trim(), settings.UnindexedCustomFileName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            string name = ReportPathResolver.Resolve(settings, direction)
                                            .FileName;

            await dialogues.ShowErrorAsync($"Both reports would have the same file name:\n{PathText.Breakable(name)}\n\nGive each tab's report its own name.");

            return null;
        }

        if (direction == AuditDirection.Unindexed
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
                await
                    dialogues.ShowErrorAsync($"The report location must be a full path, such as C:\\Reports or \\\\server\\share\\Reports:\n{PathText.Breakable(report.Directory)}");

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

        if (await Task.Run(() => File.Exists(report.FullPath)))
        {
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
        }

        return report;
    }

    // The Unindexed direction's own inputs: folders first, then the two type lists, then the three warnings in the order of milestone 4 spec §3.3.
    private async Task<bool> CheckUnindexedInputsAsync(AppSettings settings)
    {
        IReadOnlyList<ScanFolder> folders = settings.ScanFolders
                                            ??
                                            [];

        if (folders.Count == 0)
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
        ErrorOr<string[]> include = FileTypeList.Parse(string.Join(separator: ' ',
                                                                   settings.FileTypes
                                                                   ??
                                                                   []));

        ErrorOr<string[]> exclude = FileTypeList.Parse(string.Join(separator: ' ',
                                                                   settings.ExcludeTypes
                                                                   ??
                                                                   []));

        if (include.IsError
            || exclude.IsError)
        {
            await dialogues.ShowErrorAsync((include.IsError ? include : exclude).FirstError.Description);

            return false;
        }

        if (include.Value.Length == 0)
        {
            await dialogues.ShowErrorAsync(message: "Add at least one file type.");

            return false;
        }

        string? excludeEverything = exclude.Value.FirstOrDefault(entry => new FileTypePattern(entry).MatchesEverything);

        if (excludeEverything is not null)
        {
            await dialogues.ShowErrorAsync($"The Exclude list contains {excludeEverything}, which matches every file, so nothing would be checked. Remove it to run the audit.");

            return false;
        }

        return await ConfirmTypeWarningsAsync(include.Value, exclude.Value);
    }

    private async Task<bool> ConfirmTypeWarningsAsync(string[] include, string[] exclude)
    {
        string? everything = include.FirstOrDefault(entry => new FileTypePattern(entry).MatchesEverything);

        if (everything is not null
            && include.Length > 1
            && !project.RedundantTypesConfirmed)
        {
            if (!await dialogues.ConfirmAsync($"File types contains {everything}, which matches every file, so the other file types are redundant.\n\nDo you want to continue?",
                                              confirmText: "_Continue"))
            {
                return false;
            }

            project.RedundantTypesConfirmed = true;
        }

        if (everything is not null
            && exclude.Length > 0
            && !project.ExcludedTypesConfirmed)
        {
            if (!await
                     dialogues.ConfirmAsync($"File types contains {everything}, which matches every file, so every file will be checked except the excluded types.\n\nDo you want to continue?",
                                            confirmText: "_Continue"))
            {
                return false;
            }

            project.ExcludedTypesConfirmed = true;
        }

        string[] both = include.Intersect(exclude, StringComparer.Ordinal)
                               .ToArray();

        if (both.Length > 0
            && !project.OverlapConfirmed)
        {
            string entries = string.Join(separator: ' ', both);

            if (!await dialogues.ConfirmAsync($"These types are in both File types and Exclude, so they will not be checked:\n{entries}\n\nDo you want to continue?",
                                              confirmText: "_Continue"))
            {
                return false;
            }

            project.OverlapConfirmed = true;
        }

        return true;
    }

    // The first problem with the folder list, as the message to show, or null when every folder is a full path that exists and none is listed twice.
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

        foreach (ScanFolder folder in folders)
        {
            if (!seen.Add(FolderWalker.NormaliseFolder(folder.Path)))
            {
                return $"This folder is listed twice:\n{PathText.Breakable(folder.Path)}";
            }
        }

        return null;
    }

    // Creates and immediately removes the temporary file that ReportWriter uses, so the probe leaves nothing behind.
    private static ErrorOr<Success> ProbeWritable(ReportPath report)
    {
        try
        {
            using FileStream probe = new(report.FullPath + ".tmp", FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);

            return Result.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ReportErrors.FolderNotWritable(report.Directory, ex.Message);
        }
    }

    private static bool IsReadOnly(string path)
    {
        try
        {
            return File.GetAttributes(path)
                       .HasFlag(FileAttributes.ReadOnly);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Unreadable attributes are not proof of a read-only file; the write itself will report any real failure.
            return false;
        }
    }

    private static string? TryCreateFolder(string folder)
    {
        try
        {
            Directory.CreateDirectory(folder);

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return ex.Message;
        }
    }
}
