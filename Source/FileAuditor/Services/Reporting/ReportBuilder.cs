namespace FileAuditor.Services.Reporting;

/// <summary>
///     Builds the plain-text audit report (SDD §7). The output is deterministic: invariant-culture numbers, ordinal case-insensitive ordering and CRLF line endings.
/// </summary>
public static class ReportBuilder
{
    private const string NewLine = "\r\n";

    private const int LabelWidth = 17;

    private const string ColumnGap = "   ";

    private static readonly CheckOutcome[] MissingOrder = [CheckOutcome.MissingEmpty, CheckOutcome.MissingDoesNotExist];

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

    /// <summary>
    ///     Builds the report text.
    /// </summary>
    /// <param name="report">The audit to report on.</param>
    /// <returns>The report, with CRLF line endings and a trailing line break.</returns>
    public static string Build(AuditReport report)
    {
        List<string> lines = [];

        AddHeader(lines, report);
        lines.Add(string.Empty);
        AddSummary(lines, report);
        lines.Add(string.Empty);
        AddSection(lines, "Missing Files", "No missing files.", MissingOrder, report);
        lines.Add(string.Empty);
        AddSection(lines, "Unverified Files", "No unverified files.", UnverifiedOrder, report);

        StringBuilder builder = new();

        foreach (string line in lines)
        {
            builder.Append(line).Append(NewLine);
        }

        return builder.ToString();
    }

    private static void AddHeader(List<string> lines, AuditReport report)
    {
        AppSettings settings = report.Settings;
        DateTime started = report.StartedUtc.ToLocalTime();
        DateTime finished = report.FinishedUtc.ToLocalTime();

        lines.Add("File Auditor Report");
        lines.Add("===================");
        lines.Add(string.Empty);
        lines.Add(Labelled("Server:", settings.Server));
        lines.Add(Labelled("Database:", settings.Database));
        lines.Add(Labelled("Table:", settings.Table));
        lines.Add(Labelled("Path column:", settings.PathColumn));
        lines.Add(Labelled("ID column:", HasIdColumn(settings) ? settings.IdColumn : "(none)"));
        lines.Add(Labelled("Empty files:", settings.ReportEmptyFiles ? "Reported as missing" : "Not reported"));

        if (!settings.UseCustomReportLocation)
        {
            lines.Add(Labelled("Project folder:", settings.ProjectFolder));
            lines.Add(Labelled("Project stage:", settings.ProjectStage.ToString()));
            lines.Add(Labelled("Audit stage:", settings.AuditStage.ToString()));
        }

        lines.Add(string.Empty);
        lines.Add(Labelled("Started:", Timestamp(started)));
        lines.Add(Labelled("Finished:", Timestamp(finished)));
        lines.Add(Labelled("Elapsed:", Elapsed(report.FinishedUtc - report.StartedUtc)));
        lines.Add(Labelled("Result:", ResultText(report)));
    }

    private static void AddSummary(List<string> lines, AuditReport report)
    {
        long missing = report.Problems.Count(result => result.Outcome is CheckOutcome.MissingEmpty or CheckOutcome.MissingDoesNotExist);
        long unverified = report.Problems.Count - missing;

        lines.Add("Summary");
        lines.Add("-------");
        lines.Add(Labelled("Total files:", Count(report.TotalFiles)));
        lines.Add(Labelled("Found:", Count(report.FoundCount)));
        lines.Add(Labelled("Missing:", Count(missing)));
        lines.Add(Labelled("Unverified:", Count(unverified)));

        if (report.Result != AuditResultKind.Completed)
        {
            lines.Add(Labelled("Not checked:", Count(report.TotalFiles - report.FoundCount - report.Problems.Count)));
        }
    }

    private static void AddSection(List<string> lines, string title, string emptyText, CheckOutcome[] order, AuditReport report)
    {
        List<(CheckOutcome Outcome, List<CheckResult> Results)> groups = [];

        foreach (CheckOutcome outcome in order)
        {
            List<CheckResult> results = report.Problems
                .Where(result => result.Outcome == outcome)
                .OrderBy(result => result.Row.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (results.Count > 0)
            {
                groups.Add((outcome, results));
            }
        }

        int total = groups.Sum(group => group.Results.Count);
        string heading = $"{title} ({Count(total)})";

        lines.Add(heading);
        lines.Add(new string('-', heading.Length));

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
        string[] ids = results.Select(result => hasId ? $"{settings.IdColumn} {Flatten(result.Row.Id ?? string.Empty)}" : string.Empty).ToArray();
        string[] paths = results.Select(result => Flatten(result.Row.Path)).ToArray();
        string[] reasons = results.Select(result => Flatten(result.Reason ?? string.Empty)).ToArray();
        int idWidth = ids.Max(id => id.Length);
        int pathWidth = paths.Max(path => path.Length);
        int reasonWidth = reasons.Max(reason => reason.Length);

        for (int index = 0; index < results.Count; index++)
        {
            StringBuilder line = new();

            if (hasId)
            {
                line.Append(ids[index].PadRight(idWidth)).Append(ColumnGap);
            }

            line.Append(paths[index]);

            if (outcome == CheckOutcome.UnverifiedOther)
            {
                line.Append(new string(' ', pathWidth - paths[index].Length)).Append(ColumnGap);
                line.Append(reasons[index].PadRight(reasonWidth)).Append(ColumnGap);
                line.Append(Flatten(results[index].SystemMessage ?? string.Empty));
            }

            lines.Add(line.ToString().TrimEnd());
        }
    }

    private static string GroupTitle(CheckOutcome outcome) => outcome switch
    {
        CheckOutcome.MissingEmpty => "Empty",
        CheckOutcome.MissingDoesNotExist => "Does not exist",
        CheckOutcome.UnverifiedDriveOrShareNotFound => "Drive or share not found",
        CheckOutcome.UnverifiedPermissionDenied => "Permission denied",
        CheckOutcome.UnverifiedNetworkUnreachable => "Network location unreachable",
        CheckOutcome.UnverifiedPathTooLong => "Path too long",
        CheckOutcome.UnverifiedInvalidCharacters => "Invalid characters in path",
        CheckOutcome.UnverifiedPathEmpty => "Path is empty in the index table",
        CheckOutcome.UnverifiedOther => "Other errors",
        _ => outcome.ToString()
    };

    private static string ResultText(AuditReport report) => report.Result switch
    {
        AuditResultKind.Completed => "Completed",
        AuditResultKind.StoppedByUser => "Stopped by user",
        _ => $"Failed — {Flatten(report.FailureMessage ?? string.Empty)}".TrimEnd()
    };

    private static string Labelled(string label, string value) => (label.PadRight(LabelWidth) + Flatten(value)).TrimEnd();

    private static bool HasIdColumn(AppSettings settings) => !string.IsNullOrWhiteSpace(settings.IdColumn);

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Timestamp(DateTime value) => value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Elapsed(TimeSpan elapsed)
    {
        TimeSpan clamped = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        long hours = (long)clamped.TotalHours;

        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{clamped.Minutes:00}:{clamped.Seconds:00}");
    }

    // Keeps every value on one line, so a multi-line system or SQL message cannot break the layout.
    private static string Flatten(string text) => text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
}
