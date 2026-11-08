namespace FileAuditor.Services.Reporting;

/// <summary>
///     Builds the plain-text unindexed-files report (milestone 4 spec §3.7). The output is deterministic: invariant-culture numbers, ordinal case-insensitive
///     ordering and CRLF line endings.
/// </summary>
public static class UnindexedReportBuilder
{
    private static readonly CheckOutcome[] UnverifiedOrder =
    [
        CheckOutcome.UnverifiedDriveOrShareNotFound,
        CheckOutcome.UnverifiedPermissionDenied,
        CheckOutcome.UnverifiedNetworkUnreachable,
        CheckOutcome.UnverifiedPathTooLong,
        CheckOutcome.UnverifiedOther
    ];

    /// <summary>Builds the report text.</summary>
    /// <param name="report">The audit to report on.</param>
    /// <returns>The report, with CRLF line endings and a trailing line break.</returns>
    public static string Build(UnindexedReport report)
    {
        List<string> lines =
            [];

        AddHeader(lines, report);
        lines.Add(string.Empty);
        AddSummary(lines, report);
        lines.Add(string.Empty);
        AddUnindexed(lines, report);
        lines.Add(string.Empty);
        AddUnverified(lines, report);

        return ReportFormat.Join(lines);
    }

    private static void AddHeader(List<string> lines, UnindexedReport report)
    {
        AppSettings settings = report.Settings;

        lines.Add(item: "File Auditor Unindexed Report");
        lines.Add(item: "=============================");
        lines.Add(string.Empty);
        ReportFormat.AddConnectionLines(lines, settings);

        ScanFolder[] folders = settings.ScanFolders ?? [];

        for (int index = 0; index < Math.Max(folders.Length, 1); index++)
        {
            string value = folders.Length == 0 ? "(none)" : folders[index].Path + (folders[index].IncludeSubfolders ? " (with subfolders)" : string.Empty);

            lines.Add(index == 0 ? ReportFormat.Labelled(label: "Folders:", value) : ReportFormat.Continued(value));
        }

        string[] exclude = settings.ExcludeTypes ?? [];

        lines.Add(ReportFormat.Labelled(label: "File types:", FileTypeList.Format(settings.FileTypes ?? FileTypeList.Defaults)));
        lines.Add(ReportFormat.Labelled(label: "Exclude:", exclude.Length == 0 ? "(none)" : FileTypeList.Format(exclude)));
        lines.Add(ReportFormat.Labelled(label: "Grouped by:", settings.GroupByFoundExtension ? "Found extension" : "Listed type"));
        ReportFormat.AddProjectLines(lines, settings);
        ReportFormat.AddTimingLines(lines, report.StartedUtc, report.FinishedUtc, report.Kind, report.FailureMessage);
    }

    private static void AddSummary(List<string> lines, UnindexedReport report)
    {
        lines.Add(item: "Summary");
        lines.Add(item: "-------");
        lines.Add(ReportFormat.Labelled(label: "Total files:", ReportFormat.Count(report.TotalFiles)));
        lines.Add(ReportFormat.Labelled(label: "Indexed:", ReportFormat.Count(report.Indexed)));
        lines.Add(ReportFormat.Labelled(label: "Unindexed:", ReportFormat.Count(report.Unindexed.Count)));
        lines.Add(ReportFormat.Labelled(label: "Unverified:", ReportFormat.Count(report.Unverified.Count)));

        if (report.NotChecked > 0)
        {
            lines.Add(ReportFormat.Labelled(label: "Not checked:", $"{ReportFormat.Count(report.NotChecked)} (reached through links; the index could not be read for them)"));
        }
    }

    private static void AddUnindexed(List<string> lines, UnindexedReport report)
    {
        string heading = $"Unindexed Files ({ReportFormat.Count(report.Unindexed.Count)})";

        lines.Add(heading);
        lines.Add(new string(c: '-', heading.Length));

        if (report.Unindexed.Count == 0)
        {
            lines.Add(item: "No unindexed files.");

            return;
        }

        // Input folders in the order the user listed them; a folder not in the list (which the walker cannot produce) goes last.
        Dictionary<string, int> inputOrder = new(StringComparer.OrdinalIgnoreCase);

        foreach (ScanFolder folder in report.Settings.ScanFolders ?? [])
        {
            inputOrder.TryAdd(folder.Path, inputOrder.Count);
        }

        bool groupByFound = report.Settings.GroupByFoundExtension;

        List<IGrouping<string, ScannedFile>> byFolder = report.Unindexed
                                                              .GroupBy(file => file.InputFolder, StringComparer.OrdinalIgnoreCase)
                                                              .OrderBy(group => inputOrder.GetValueOrDefault(group.Key, int.MaxValue))
                                                              .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                                                              .ThenBy(group => group.Key, StringComparer.Ordinal)
                                                              .ToList();

        for (int folderIndex = 0; folderIndex < byFolder.Count; folderIndex++)
        {
            if (folderIndex > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"[{ReportFormat.Flatten(byFolder[folderIndex].Key)} ({ReportFormat.Count(byFolder[folderIndex].Count())})]");

            List<IGrouping<string, ScannedFile>> byType = byFolder[folderIndex]
                                                          .GroupBy(file => TypeKey(file, groupByFound), StringComparer.Ordinal)
                                                          .OrderBy(group => group.Key, StringComparer.Ordinal)
                                                          .ToList();

            foreach (IGrouping<string, ScannedFile> group in byType)
            {
                lines.Add(string.Empty);
                lines.Add($"  [{ReportFormat.Flatten(group.Key)} ({ReportFormat.Count(group.Count())})]");

                foreach (ScannedFile file in group.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                                                  .ThenBy(file => file.Path, StringComparer.Ordinal))
                {
                    lines.Add("  " + ReportFormat.Flatten(file.Path));
                }
            }
        }
    }

    private static string TypeKey(ScannedFile file, bool groupByFound)
        => groupByFound ? file.Type.FoundExtension(Path.GetFileName(file.Path)) : file.Type.Text;

    private static void AddUnverified(List<string> lines, UnindexedReport report)
    {
        string heading = $"Unverified Folders ({ReportFormat.Count(report.Unverified.Count)})";

        lines.Add(heading);
        lines.Add(new string(c: '-', heading.Length));

        if (report.Unverified.Count == 0)
        {
            lines.Add(item: "No unverified folders.");

            return;
        }

        bool first = true;

        foreach (CheckOutcome group in UnverifiedOrder)
        {
            List<UnverifiedFolder> folders = report.Unverified
                                                   .Where(folder => GroupOf(folder) == group)
                                                   .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                                                   .ThenBy(folder => folder.Path, StringComparer.Ordinal)
                                                   .ThenBy(folder => folder.ReasonText    ?? string.Empty, StringComparer.Ordinal)
                                                   .ThenBy(folder => folder.SystemMessage ?? string.Empty, StringComparer.Ordinal)
                                                   .ToList();

            if (folders.Count == 0)
            {
                continue;
            }

            if (!first)
            {
                lines.Add(string.Empty);
            }

            first = false;

            lines.Add($"[{GroupTitle(group)} ({ReportFormat.Count(folders.Count)})]");
            AddFolderLines(lines, group, folders);
        }
    }

    private static void AddFolderLines(List<string> lines, CheckOutcome group, List<UnverifiedFolder> folders)
    {
        string[] paths = folders.Select(folder => ReportFormat.Flatten(folder.Path))
                                .ToArray();

        if (group != CheckOutcome.UnverifiedOther)
        {
            lines.AddRange(paths);

            return;
        }

        string[] reasons = folders.Select(folder => ReportFormat.Flatten(ReasonOf(folder)))
                                  .ToArray();

        int pathWidth   = paths.Max(path => path.Length);
        int reasonWidth = reasons.Max(reason => reason.Length);

        for (int index = 0; index < folders.Count; index++)
        {
            StringBuilder line = new();

            line.Append(paths[index])
                .Append(new string(c: ' ', pathWidth - paths[index].Length))
                .Append(ReportFormat.ColumnGap)
                .Append(reasons[index]
                            .PadRight(reasonWidth))
                .Append(ReportFormat.ColumnGap)
                .Append(ReportFormat.Flatten(folders[index].SystemMessage ?? string.Empty));

            lines.Add(line.ToString()
                          .TrimEnd());
        }
    }

    // The report has five groups; every other reason (invalid characters, anything unforeseen) is an Other error that carries its own reason text.
    private static CheckOutcome GroupOf(UnverifiedFolder folder)
        => folder.Reason is CheckOutcome.UnverifiedDriveOrShareNotFound
                            or CheckOutcome.UnverifiedPermissionDenied
                            or CheckOutcome.UnverifiedNetworkUnreachable
                            or CheckOutcome.UnverifiedPathTooLong
               ? folder.Reason
               : CheckOutcome.UnverifiedOther;

    private static string ReasonOf(UnverifiedFolder folder)
        => folder.ReasonText
           ?? folder.Reason switch
           {
               CheckOutcome.UnverifiedInvalidCharacters => "Invalid characters in path",
               var _                                    => "I/O error"
           };

    private static string GroupTitle(CheckOutcome group)
        => group switch
        {
            CheckOutcome.UnverifiedDriveOrShareNotFound => "Drive or share not found",
            CheckOutcome.UnverifiedPermissionDenied     => "Permission denied",
            CheckOutcome.UnverifiedNetworkUnreachable   => "Network location unreachable",
            CheckOutcome.UnverifiedPathTooLong          => "Path too long",
            var _                                       => "Other errors"
        };
}
