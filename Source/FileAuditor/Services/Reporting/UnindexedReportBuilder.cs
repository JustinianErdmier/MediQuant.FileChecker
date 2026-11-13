namespace FileAuditor.Services.Reporting;

/// <summary>Builds the plain-text unindexed-files report. The output is deterministic: invariant-culture numbers, ordinal case-insensitive ordering and CRLF line endings.</summary>
public static class UnindexedReportBuilder
{
    /// <summary>The order in which the Unverified groups are listed.</summary>
    private static readonly CheckOutcomes[] UnverifiedOrder =
    [
        CheckOutcomes.UnverifiedDriveOrShareNotFound,
        CheckOutcomes.UnverifiedPermissionDenied,
        CheckOutcomes.UnverifiedNetworkUnreachable,
        CheckOutcomes.UnverifiedPathTooLong,
        CheckOutcomes.UnverifiedLinkNotFollowed,
        CheckOutcomes.UnverifiedOther
    ];

    /// <summary>Builds the report text.</summary>
    /// <param name="report">The audit to report on.</param>
    /// <returns>The report, with CRLF line endings and a trailing line break.</returns>
    public static string Build(UnindexedReport report)
    {
        List<string> lines = [];

        AddHeader(lines, report);

        lines.Add(string.Empty);

        AddSummary(lines, report);

        lines.Add(string.Empty);

        AddUnindexed(lines, report);

        lines.Add(string.Empty);

        AddUnverified(lines, report);

        return ReportFormat.Join(lines);
    }

    /// <summary>Adds the title and the settings the run used.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddHeader(List<string> lines, UnindexedReport report)
    {
        AppSettings settings = report.Settings;

        lines.Add(item: "File Auditor Unindexed Report");
        lines.Add(item: "=============================");
        lines.Add(string.Empty);

        ReportFormat.AddConnectionLines(lines, settings);

        ScanFolder[] folders = settings.ScanFolders;

        for (int index = 0; index < Math.Max(folders.Length, val2: 1); index++)
        {
            string value = folders is []
                               ? "(none)"
                               : $"{folders[index].Path}{(folders[index].IncludeSubfolders ? " (with subfolders)" : string.Empty)}";

            lines.Add(index is 0
                          ? ReportFormat.Labelled(label: "Folders:", value)
                          : ReportFormat.Continued(value));
        }

        string[] exclude = settings.ExcludeTypes;

        lines.Add(ReportFormat.Labelled(label: "File types:", FileTypeList.Format(settings.FileTypes)));
        lines.Add(ReportFormat.Labelled(label: "Exclude:", exclude is [] ? "(none)" : FileTypeList.Format(exclude)));
        lines.Add(ReportFormat.Labelled(label: "Grouped by:", settings.GroupByFoundExtension ? "Found extension" : "Listed type"));
        ReportFormat.AddProjectLines(lines, settings);
        ReportFormat.AddTimingLines(lines, report.StartedUtc, report.FinishedUtc, report.Result, report.FailureMessage);
    }

    /// <summary>Adds the Summary block: the counts of every kind of result.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddSummary(List<string> lines, UnindexedReport report)
    {
        lines.Add(item: "Summary");
        lines.Add(item: "-------");
        lines.Add(ReportFormat.Labelled(label: "Total files:", ReportFormat.Count(report.TotalFiles)));
        lines.Add(ReportFormat.Labelled(label: "Excluded:", ReportFormat.Count(report.Excluded)));
        lines.Add(ReportFormat.Labelled(label: "Index paths:", ReportFormat.Count(report.IndexPaths)));
        lines.Add(ReportFormat.Labelled(label: "Indexed:", ReportFormat.Count(report.Indexed)));
        lines.Add(ReportFormat.Labelled(label: "Unindexed:", ReportFormat.Count(report.Unindexed.Count)));
        lines.Add(ReportFormat.Labelled(label: "Unverified:", ReportFormat.Count(report.Unverified.Count)));
    }

    /// <summary>Adds the Unindexed Files section: the files grouped by input folder, in the order the user listed them, and then by file type.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddUnindexed(List<string> lines, UnindexedReport report)
    {
        string heading = $"Unindexed Files ({ReportFormat.Count(report.Unindexed.Count)})";

        lines.Add(heading);
        lines.Add(new string(c: '-', heading.Length));

        if (report.Unindexed is [])
        {
            lines.Add(item: "No unindexed files.");

            return;
        }

        // Input folders in the order the user listed them; a folder not in the list (which the walker cannot produce) goes last.
        Dictionary<string, int> inputOrder = new(StringComparer.OrdinalIgnoreCase);

        foreach (ScanFolder folder in report.Settings.ScanFolders)
        {
            inputOrder.TryAdd(folder.Path, inputOrder.Count);
        }

        bool groupByFound = report.Settings.GroupByFoundExtension;

        List<IGrouping<string, ScannedFile>> byFolder =
        [
            .. report.Unindexed
                     .GroupBy(file => file.InputFolder, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => inputOrder.GetValueOrDefault(group.Key, int.MaxValue))
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(group => group.Key, StringComparer.Ordinal)
        ];

        for (int folderIndex = 0; folderIndex < byFolder.Count; folderIndex++)
        {
            if (folderIndex > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"[{ReportFormat.Flatten(byFolder[folderIndex].Key)} ({ReportFormat.Count(byFolder[folderIndex].Count())})]");

            List<IGrouping<string, ScannedFile>> byType =
            [
                .. byFolder[folderIndex]
                   .GroupBy(file => TypeKey(file, groupByFound), StringComparer.Ordinal)
                   .OrderBy(group => group.Key, StringComparer.Ordinal)
            ];

            foreach (IGrouping<string, ScannedFile> group in byType)
            {
                lines.Add(string.Empty);
                lines.Add($"  [{ReportFormat.Flatten(group.Key)} ({ReportFormat.Count(group.Count())})]");

                lines.AddRange(group.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
                                    .ThenBy(file => file.Path, StringComparer.Ordinal)
                                    .Select(file => $"  {ReportFormat.Flatten(file.Path)}"));
            }
        }
    }

    /// <summary>Gets the name of the file type group a file is listed under.</summary>
    /// <param name="file">The file.</param>
    /// <param name="groupByFound">Whether files are grouped by the extension found rather than the listed type.</param>
    /// <returns>The extension found, or the listed type's text.</returns>
    private static string TypeKey(ScannedFile file, bool groupByFound)
        => groupByFound
               ? file.Type.FoundExtension(Path.GetFileName(file.Path))
               : file.Type.Text;

    /// <summary>Adds the Unverified section: the folders that could not be listed and the links that were not followed, in one group for each reason.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddUnverified(List<string> lines, UnindexedReport report)
    {
        string heading = $"Unverified ({ReportFormat.Count(report.Unverified.Count)})";

        lines.Add(heading);
        lines.Add(new string(c: '-', heading.Length));

        if (report.Unverified is [])
        {
            lines.Add(item: "No unverified folders or links.");

            return;
        }

        bool isFirst = true;

        foreach (CheckOutcomes group in UnverifiedOrder)
        {
            List<UnverifiedFolder> folders =
            [
                .. report.Unverified
                         .Where(folder => GroupOf(folder) == group)
                         .OrderBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(folder => folder.Path, StringComparer.Ordinal)
                         .ThenBy(folder => folder.ReasonText    ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(folder => folder.SystemMessage ?? string.Empty, StringComparer.Ordinal)
            ];

            if (folders is [])
            {
                continue;
            }

            if (!isFirst)
            {
                lines.Add(string.Empty);
            }

            isFirst = false;

            lines.Add($"[{GroupTitle(group)} ({ReportFormat.Count(folders.Count)})]");

            AddFolderLines(lines, group, folders);
        }
    }

    /// <summary>Adds the lines of one Unverified group: the paths alone, or for Other errors the path, the reason and the system message in aligned columns.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="group">The group being listed.</param>
    /// <param name="folders">The group's folders and links.</param>
    private static void AddFolderLines(List<string> lines, CheckOutcomes group, List<UnverifiedFolder> folders)
    {
        string[] paths = [.. folders.Select(folder => ReportFormat.Flatten(folder.Path))];

        if (group is not CheckOutcomes.UnverifiedOther)
        {
            lines.AddRange(paths);

            return;
        }

        string[] reasons = [.. folders.Select(folder => ReportFormat.Flatten(ReasonOf(folder)))];

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

    /// <summary>
    ///     Gets the group a folder is listed under. The report has six groups: five reasons have their own, and <see cref="CheckOutcomes.UnverifiedInvalidCharacters" />,
    ///     <see cref="CheckOutcomes.UnverifiedPathEmpty" /> and <see cref="CheckOutcomes.UnverifiedOther" /> are all listed as Other errors, which carry their own reason text.
    /// </summary>
    /// <param name="folder">The folder or link.</param>
    /// <returns>The group's outcome.</returns>
    private static CheckOutcomes GroupOf(UnverifiedFolder folder)
        => folder.Reason switch
        {
            CheckOutcomes.UnverifiedDriveOrShareNotFound
                or CheckOutcomes.UnverifiedPermissionDenied
                or CheckOutcomes.UnverifiedNetworkUnreachable
                or CheckOutcomes.UnverifiedPathTooLong
                or CheckOutcomes.UnverifiedLinkNotFollowed => folder.Reason,
            CheckOutcomes.UnverifiedInvalidCharacters or CheckOutcomes.UnverifiedPathEmpty or CheckOutcomes.UnverifiedOther => CheckOutcomes.UnverifiedOther,
            var _                                                                                                           => throw new UnreachableException()
        };

    /// <summary>Gets the reason shown on an Other errors line.</summary>
    /// <param name="folder">The folder or link.</param>
    /// <returns>The reason text the walker recorded, or the standard text for the outcome.</returns>
    private static string ReasonOf(UnverifiedFolder folder)
        => folder.ReasonText
           ?? folder.Reason switch
           {
               CheckOutcomes.UnverifiedInvalidCharacters => "Invalid characters in path",
               CheckOutcomes.UnverifiedDriveOrShareNotFound
                   or CheckOutcomes.UnverifiedPermissionDenied
                   or CheckOutcomes.UnverifiedNetworkUnreachable
                   or CheckOutcomes.UnverifiedPathTooLong
                   or CheckOutcomes.UnverifiedLinkNotFollowed
                   or CheckOutcomes.UnverifiedPathEmpty
                   or CheckOutcomes.UnverifiedOther => "I/O error",
               var _ => throw new UnreachableException()
           };

    /// <summary>Gets the heading of a group.</summary>
    /// <param name="group">The group's outcome.</param>
    /// <returns>The heading text, without the count.</returns>
    private static string GroupTitle(CheckOutcomes group)
        => group switch
        {
            CheckOutcomes.UnverifiedDriveOrShareNotFound => "Drive or share not found",
            CheckOutcomes.UnverifiedPermissionDenied     => "Permission denied",
            CheckOutcomes.UnverifiedNetworkUnreachable   => "Network location unreachable",
            CheckOutcomes.UnverifiedPathTooLong          => "Path too long",
            CheckOutcomes.UnverifiedLinkNotFollowed      => "Link not followed",
            CheckOutcomes.UnverifiedOther                => "Other errors",
            var _                                        => throw new UnreachableException()
        };
}
