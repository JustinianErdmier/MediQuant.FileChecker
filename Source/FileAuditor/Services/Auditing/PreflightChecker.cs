namespace FileAuditor.Services.Auditing;

/// <summary>
///     The checks that run when Start is pressed, before the database is touched (SDD §6.1). Each failure or declined confirmation is explained in a dialogue.
/// </summary>
/// <param name="dialogues">Shows the errors and confirmations.</param>
public sealed class PreflightChecker(DialogueService dialogues)
{
    /// <summary>
    ///     Runs the checks in order: identifiers, custom file name, custom location form, report folder, existing report.
    /// </summary>
    /// <param name="settings">The settings to audit with.</param>
    /// <returns>The resolved report path when the run may start; <see langword="null" /> when a check failed or the user declined (the user has been told).</returns>
    public async Task<ReportPath?> RunAsync(AppSettings settings)
    {
        ErrorOr<AuditQuery> query = QueryBuilder.Build(settings.Table, settings.PathColumn, settings.IdColumn);

        if (query.IsError)
        {
            await dialogues.ShowErrorAsync(query.FirstError.Description);

            return null;
        }

        ReportPath report = ReportPathResolver.Resolve(settings);

        if (report.IsCustom)
        {
            if (report.FileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                await dialogues.ShowErrorAsync($"The report file name contains characters that are not allowed:\n{report.FileName}");

                return null;
            }

            if (!Path.IsPathFullyQualified(report.Directory))
            {
                await dialogues.ShowErrorAsync($"The report location must be a full path, such as C:\\Reports or \\\\server\\share\\Reports:\n{report.Directory}");

                return null;
            }
        }

        // The report folder may be on a slow or disconnected drive, so none of these probes run on the UI thread.
        if (!await Task.Run(() => Directory.Exists(report.Directory)))
        {
            if (!report.IsCustom)
            {
                await dialogues.ShowErrorAsync($"The report folder does not exist:\n{report.Directory}");

                return null;
            }

            if (!await dialogues.ConfirmAsync($"The report folder does not exist:\n{report.Directory}\n\nDo you want to create it?", "_Create Folder"))
            {
                return null;
            }

            string? failure = await Task.Run(() => TryCreateFolder(report.Directory));

            if (failure is not null)
            {
                await dialogues.ShowErrorAsync($"The report folder could not be created:\n{report.Directory}\n\n{failure}");

                return null;
            }
        }

        if (await Task.Run(() => File.Exists(report.FullPath)))
        {
            if (!await dialogues.ConfirmAsync($"The report already exists:\n{report.FullPath}\n\nDo you want to overwrite it?", "_Overwrite"))
            {
                return null;
            }
        }

        return report;
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
