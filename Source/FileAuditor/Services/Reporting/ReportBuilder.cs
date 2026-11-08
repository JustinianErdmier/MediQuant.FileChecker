namespace FileAuditor.Services.Reporting;

/// <summary>Builds the plain-text audit report (SDD §7). The output is deterministic: invariant-culture numbers, ordinal case-insensitive ordering and CRLF line endings.</summary>
public static class ReportBuilder
{
    private const string ColumnGap = ReportFormat.ColumnGap;

    private static readonly CheckOutcome[] MissingOrder =
    [
        CheckOutcome.MissingEmpty,
        CheckOutcome.MissingDoesNotExist
    ];

    private static readonly CheckOutcome[] UnverifiedOrder =
    [
        CheckOutcome.UnverifiedDriveOrShareNotFound,
        CheckOutcome.UnverifiedPermissionDenied,
        CheckOutcome.UnverifiedNetworkUnreachable,
        CheckOutcome.UnverifiedPathTooLong,
        CheckOutcome.UnverifiedInvalidCharacters,
        CheckOutcome.UnverifiedPathEmpty,
        CheckOutcome.UnverifiedOther
    ];

    /// <summary>Builds the report text.</summary>
    /// <param name="report">The audit to report on.</param>
    /// <returns>The report, with CRLF line endings and a trailing line break.</returns>
    public static string Build(AuditReport report)
    {
        List<string> lines =
            [];

        AddHeader(lines, report);
        lines.Add(string.Empty);
        AddSummary(lines, report);
        lines.Add(string.Empty);
        AddSection(lines, title: "Missing Files", emptyText: "No missing files.", MissingOrder, report);
        lines.Add(string.Empty);
        AddSection(lines, title: "Unverified Files", emptyText: "No unverified files.", UnverifiedOrder, report);

        return ReportFormat.Join(lines);
    }

    private static void AddHeader(List<string> lines, AuditReport report)
    {
        AppSettings settings = report.Settings;

        lines.Add(item: "File Auditor Missing Report");
        lines.Add(item: "===========================");
        lines.Add(string.Empty);
        ReportFormat.AddConnectionLines(lines, settings);
        lines.Add(Labelled(label: "ID column:", HasIdColumn(settings) ? settings.IdColumn : "(none)"));
        lines.Add(Labelled(label: "Empty files:", settings.ReportEmptyFiles ? "Reported as missing" : "Not reported"));
        ReportFormat.AddProjectLines(lines, settings);
        ReportFormat.AddTimingLines(lines, report.StartedUtc, report.FinishedUtc, report.Result, report.FailureMessage);
    }

    private static void AddSummary(List<string> lines, AuditReport report)
    {
        long missing    = report.Problems.Count(result => result.Outcome is CheckOutcome.MissingEmpty or CheckOutcome.MissingDoesNotExist);
        long unverified = report.Problems.Count - missing;

        lines.Add(item: "Summary");
        lines.Add(item: "-------");
        lines.Add(Labelled(label: "Total files:", Count(report.TotalFiles)));
        lines.Add(Labelled(label: "Found:", Count(report.FoundCount)));
        lines.Add(Labelled(label: "Missing:", Count(missing)));
        lines.Add(Labelled(label: "Unverified:", Count(unverified)));

        if (report.Result != AuditResultKind.Completed)
        {
            lines.Add(Labelled(label: "Not checked:", Count(report.TotalFiles - report.FoundCount - report.Problems.Count)));
        }
    }

    private static void AddSection(List<string> lines, string title, string emptyText, CheckOutcome[] order, AuditReport report)
    {
        List<(CheckOutcome Outcome, List<CheckResult> Results)> groups =
            [];

        foreach (CheckOutcome outcome in order)
        {
            List<CheckResult> results = report.Problems
                                              .Where(result => result.Outcome == outcome)
                                              .OrderBy(result => result.Row.Path, StringComparer.OrdinalIgnoreCase)
                                              .ThenBy(result => result.Row.Path, StringComparer.Ordinal)
                                              .ThenBy(result => result.Row.Id        ?? string.Empty, StringComparer.Ordinal)
                                              .ThenBy(result => result.Reason        ?? string.Empty, StringComparer.Ordinal)
                                              .ThenBy(result => result.SystemMessage ?? string.Empty, StringComparer.Ordinal)
                                              .ToList();

            if (results.Count > 0)
            {
                groups.Add((outcome, results));
            }
        }

        int    total   = groups.Sum(group => group.Results.Count);
        string heading = $"{title} ({Count(total)})";

        lines.Add(heading);
        lines.Add(new string(c: '-', heading.Length));

        if (groups.Count == 0)
        {
            lines.Add(emptyText);

            return;
        }

        for (int index = 0; index < groups.Count; index++)
        {
            if (index > 0)
            {
                lines.Add(string.Empty);
            }

            lines.Add($"[{GroupTitle(groups[index].Outcome)} ({Count(groups[index].Results.Count)})]");
            AddFileLines(lines, groups[index].Outcome, groups[index].Results, report.Settings);
        }
    }

    private static void AddFileLines(List<string> lines, CheckOutcome outcome, List<CheckResult> results, AppSettings settings)
    {
        bool hasId = HasIdColumn(settings);

        string[] ids = results.Select(result => hasId ? $"{settings.IdColumn} {Flatten(result.Row.Id ?? string.Empty)}" : string.Empty)
                              .ToArray();

        string[] paths = results.Select(result => Flatten(result.Row.Path))
                                .ToArray();

        string[] reasons = results.Select(result => Flatten(result.Reason ?? string.Empty))
                                  .ToArray();

        int idWidth     = ids.Max(id => id.Length);
        int pathWidth   = paths.Max(path => path.Length);
        int reasonWidth = reasons.Max(reason => reason.Length);

        for (int index = 0; index < results.Count; index++)
        {
            StringBuilder line = new();

            if (hasId)
            {
                line.Append(ids[index]
                                .PadRight(idWidth))
                    .Append(ColumnGap);
            }

            line.Append(paths[index]);

            if (outcome == CheckOutcome.UnverifiedOther)
            {
                line.Append(new string(c: ' ', pathWidth - paths[index].Length))
                    .Append(ColumnGap);

                line.Append(reasons[index]
                                .PadRight(reasonWidth))
                    .Append(ColumnGap);

                line.Append(Flatten(results[index].SystemMessage ?? string.Empty));
            }

            lines.Add(line.ToString()
                          .TrimEnd());
        }
    }

    private static string GroupTitle(CheckOutcome outcome)
        => outcome switch
        {
            CheckOutcome.MissingEmpty                   => "Empty",
            CheckOutcome.MissingDoesNotExist            => "Does not exist",
            CheckOutcome.UnverifiedDriveOrShareNotFound => "Drive or share not found",
            CheckOutcome.UnverifiedPermissionDenied     => "Permission denied",
            CheckOutcome.UnverifiedNetworkUnreachable   => "Network location unreachable",
            CheckOutcome.UnverifiedPathTooLong          => "Path too long",
            CheckOutcome.UnverifiedInvalidCharacters    => "Invalid characters in path",
            CheckOutcome.UnverifiedPathEmpty            => "Path is empty in the index table",
            CheckOutcome.UnverifiedOther                => "Other errors",
            var _                                       => outcome.ToString()
        };

    private static string Labelled(string label, string value) => ReportFormat.Labelled(label, value);

    private static bool HasIdColumn(AppSettings settings) => !string.IsNullOrWhiteSpace(settings.IdColumn);

    private static string Count(long value) => ReportFormat.Count(value);

    private static string Flatten(string text) => ReportFormat.Flatten(text);
}
