namespace FileAuditor.Services.Reporting;

/// <summary>Builds the plain-text audit report. The output is deterministic: invariant-culture numbers, ordinal case-insensitive ordering and CRLF line endings.</summary>
public static class ReportBuilder
{
    /// <summary>The gap between the columns of an Other errors line.</summary>
    private const string ColumnGap = ReportFormat.ColumnGap;

    /// <summary>The order in which the Missing outcomes are listed.</summary>
    private static readonly CheckOutcomes[] MissingOrder = [CheckOutcomes.MissingEmpty, CheckOutcomes.MissingDoesNotExist];

    /// <summary>The order in which the Unverified outcomes are listed.</summary>
    private static readonly CheckOutcomes[] UnverifiedOrder =
    [
        CheckOutcomes.UnverifiedDriveOrShareNotFound,
        CheckOutcomes.UnverifiedPermissionDenied,
        CheckOutcomes.UnverifiedNetworkUnreachable,
        CheckOutcomes.UnverifiedPathTooLong,
        CheckOutcomes.UnverifiedInvalidCharacters,
        CheckOutcomes.UnverifiedPathEmpty,
        CheckOutcomes.UnverifiedOther
    ];

    /// <summary>Builds the report text.</summary>
    /// <param name="report">The audit to report on.</param>
    /// <returns>The report, with CRLF line endings and a trailing line break.</returns>
    public static string Build(AuditReport report)
    {
        List<string> lines = [];

        AddHeader(lines, report);

        lines.Add(string.Empty);

        AddSummary(lines, report);

        lines.Add(string.Empty);

        AddSection(lines, title: "Missing Files", emptyText: "No missing files.", MissingOrder, report);

        lines.Add(string.Empty);

        AddSection(lines, title: "Unverified Files", emptyText: "No unverified files.", UnverifiedOrder, report);

        return ReportFormat.Join(lines);
    }

    /// <summary>Adds the title and the settings the run used.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
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

    /// <summary>Adds the Summary block: the counts of every kind of row and result.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddSummary(List<string> lines, AuditReport report)
    {
        RowCounts counts = report.Counts;

        long missing    = report.Problems.Count(result => result.Outcome is CheckOutcomes.MissingEmpty or CheckOutcomes.MissingDoesNotExist);
        long unverified = report.Problems.Count - missing;

        string unverifiedText = counts.EmptyPaths > 0 ? $"{Count(unverified)} ({Count(counts.EmptyPaths)})" : Count(unverified);

        lines.Add(item: "Summary");
        lines.Add(item: "-------");
        lines.Add(Labelled(label: "Total files:", Count(counts.TableRows)));
        lines.Add(Labelled(label: "Distinct:", Count(counts.Distinct)));
        lines.Add(Labelled(label: "Duplicates:", Count(counts.Duplicates)));
        lines.Add(Labelled(label: "Found:", Count(report.FoundCount)));
        lines.Add(Labelled(label: "Missing:", Count(missing)));
        lines.Add(Labelled(label: "Unverified:", unverifiedText));
        lines.Add(Labelled(label: "Skipped:", Count(counts.Skipped)));

        if (report.Result is not AuditResultKinds.Completed)
        {
            // The rows never read plus the distinct rows read but not checked; never shown below zero (a table that changes during the run is out of scope).
            long notChecked = counts.TableRows - (report.FoundCount + report.Problems.Count + counts.Duplicates + counts.EmptyPaths + counts.Skipped);

            lines.Add(Labelled(label: "Not checked:", Count(Math.Max(val1: 0, notChecked))));
        }
    }

    /// <summary>Adds one section, with a heading and a group for each outcome that has files, or the empty text when none does.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="title">The section's title.</param>
    /// <param name="emptyText">The line shown when the section has no files.</param>
    /// <param name="order">The outcomes the section lists, in the order they appear.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddSection(List<string> lines, string title, string emptyText, CheckOutcomes[] order, AuditReport report)
    {
        List<(CheckOutcomes Outcome, List<CheckResult> Results)> groups =
        [
            .. from outcome in order
               let results = (List<CheckResult>)
               [
                   .. report.Problems.Where(result => result.Outcome == outcome)
                            .OrderBy(result => result.Row.Path, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(result => result.Row.Path, StringComparer.Ordinal)
                            .ThenBy(result => result.Row.Id        ?? string.Empty, StringComparer.Ordinal)
                            .ThenBy(result => result.Reason        ?? string.Empty, StringComparer.Ordinal)
                            .ThenBy(result => result.SystemMessage ?? string.Empty, StringComparer.Ordinal)
               ]
               where results.Count > 0
               select (outcome, results)
        ];

        // The empty-path group sits in the Unverified order but is built from the empty-path count and IDs, not from the problems.
        bool hasEmptyGroup = order.Contains(CheckOutcomes.UnverifiedPathEmpty) && report.Counts.EmptyPaths > 0;

        long   total   = groups.Sum(group => (long)group.Results.Count) + (hasEmptyGroup ? report.Counts.EmptyPaths : 0);
        string heading = $"{title} ({Count(total)})";

        lines.Add(heading);
        lines.Add(new string(c: '-', heading.Length));

        if (groups.Count is 0 && !hasEmptyGroup)
        {
            lines.Add(emptyText);

            return;
        }

        bool first = true;

        foreach (CheckOutcomes outcome in order)
        {
            if (outcome is CheckOutcomes.UnverifiedPathEmpty && hasEmptyGroup)
            {
                if (!first)
                {
                    lines.Add(string.Empty);
                }

                first = false;

                AddEmptyPathGroup(lines, report);
            }

            List<CheckResult>? results = groups.Where(group => group.Outcome == outcome)
                                               .Select(group => group.Results)
                                               .FirstOrDefault();

            if (results is null)
            {
                continue;
            }

            if (!first)
            {
                lines.Add(string.Empty);
            }

            first = false;

            lines.Add($"[{GroupTitle(outcome)} ({Count(results.Count)})]");

            AddFileLines(lines, outcome, results, report.Settings);
        }
    }

    /// <summary>Adds the Path is empty in the index table group: one line per ID read, or the single note that says there is no ID column.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="report">The audit to report on.</param>
    private static void AddEmptyPathGroup(List<string> lines, AuditReport report)
    {
        lines.Add($"[{GroupTitle(CheckOutcomes.UnverifiedPathEmpty)} ({Count(report.Counts.EmptyPaths)})]");

        if (report.EmptyPathIds is null)
        {
            lines.Add(item: "Missing filepaths not listed because no ID column was specified");

            return;
        }

        string[] ids = [.. report.EmptyPathIds.Order(StringComparer.OrdinalIgnoreCase).ThenBy(id => id, StringComparer.Ordinal)];

        foreach (string id in ids)
        {
            lines.Add($"{report.Settings.IdColumn} {Flatten(id)}".TrimEnd());
        }
    }

    /// <summary>Adds the file lines of one group: the optional ID, the path and, for Other errors, the reason and the system message, each in its own aligned column.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="outcome">The outcome the group lists.</param>
    /// <param name="results">The group's files.</param>
    /// <param name="settings">The settings the run used.</param>
    private static void AddFileLines(List<string> lines, CheckOutcomes outcome, List<CheckResult> results, AppSettings settings)
    {
        bool hasId = HasIdColumn(settings);

        string[] ids = [.. results.Select(result => hasId ? $"{settings.IdColumn} {Flatten(result.Row.Id ?? string.Empty)}" : string.Empty)];

        string[] paths = [.. results.Select(result => Flatten(result.Row.Path))];

        string[] reasons = [.. results.Select(result => Flatten(result.Reason ?? string.Empty))];

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

            if (outcome is CheckOutcomes.UnverifiedOther)
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

    /// <summary>Gets the heading of a group.</summary>
    /// <param name="outcome">The group's outcome.</param>
    /// <returns>The heading text, without the count.</returns>
    private static string GroupTitle(CheckOutcomes outcome)
        => outcome switch
        {
            CheckOutcomes.MissingEmpty                   => "Empty",
            CheckOutcomes.MissingDoesNotExist            => "Does not exist",
            CheckOutcomes.UnverifiedDriveOrShareNotFound => "Drive or share not found",
            CheckOutcomes.UnverifiedPermissionDenied     => "Permission denied",
            CheckOutcomes.UnverifiedNetworkUnreachable   => "Network location unreachable",
            CheckOutcomes.UnverifiedPathTooLong          => "Path too long",
            CheckOutcomes.UnverifiedInvalidCharacters    => "Invalid characters in path",
            CheckOutcomes.UnverifiedPathEmpty            => "Path is empty in the index table",
            CheckOutcomes.UnverifiedOther                => "Other errors",
            var _                                        => throw new UnreachableException()
        };

    /// <summary>Formats a labelled value; see <see cref="ReportFormat.Labelled" />.</summary>
    /// <param name="label">The label, with its colon.</param>
    /// <param name="value">The value.</param>
    /// <returns>The line, without trailing white space.</returns>
    private static string Labelled(string label, string value) => ReportFormat.Labelled(label, value);

    /// <summary>Determines whether the settings name an ID column.</summary>
    /// <param name="settings">The settings the run used.</param>
    /// <returns><see langword="true" /> when an ID column is configured.</returns>
    private static bool HasIdColumn(AppSettings settings) => !string.IsNullOrWhiteSpace(settings.IdColumn);

    /// <summary>Formats a count; see <see cref="ReportFormat.Count" />.</summary>
    /// <param name="value">The count.</param>
    /// <returns>The text, with thousands separators.</returns>
    private static string Count(long value) => ReportFormat.Count(value);

    /// <summary>Keeps a value on one line; see <see cref="ReportFormat.Flatten" />.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text on one line.</returns>
    private static string Flatten(string text) => ReportFormat.Flatten(text);
}
