namespace FileAuditor.Services.Reporting;

/// <summary>The formatting rules both plain-text reports share (SDD §7): label padding, invariant counts, local timestamps, one-line values and the common header lines.</summary>
public static class ReportFormat
{
    /// <summary>The width, in characters, used to align report labels and continued values.</summary>
    private const int LabelWidth = 17;

    /// <summary>The gap between the columns of an Other errors line.</summary>
    public const string ColumnGap = "   ";

    /// <summary>The line ending of every report.</summary>
    public const string NewLine = "\r\n";

    /// <summary>Joins the lines into the report text.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <returns>The report, with CRLF line endings and a trailing line break.</returns>
    public static string Join(IEnumerable<string> lines)
    {
        StringBuilder stringBuilder = new();

        foreach (string line in lines)
        {
            stringBuilder.Append(line)
                         .Append(NewLine);
        }

        return stringBuilder.ToString();
    }

    /// <summary>Adds the Server, Database, Table and Path column lines.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="settings">The settings the run used.</param>
    public static void AddConnectionLines(List<string> lines, AppSettings settings)
    {
        lines.Add(Labelled(label: "Server:", settings.Server));
        lines.Add(Labelled(label: "Database:", settings.Database));
        lines.Add(Labelled(label: "Table:", settings.Table));
        lines.Add(Labelled(label: "Path column:", settings.PathColumn));
    }

    /// <summary>Adds the Project folder, Project stage and Audit stage lines; a custom report location has no project lines.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="settings">The settings the run used.</param>
    public static void AddProjectLines(List<string> lines, AppSettings settings)
    {
        if (settings.UseCustomReportLocation)
        {
            return;
        }

        lines.Add(Labelled(label: "Project folder:", settings.ProjectFolder));
        lines.Add(Labelled(label: "Project stage:", settings.ProjectStages.ToString()));
        lines.Add(Labelled(label: "Audit stage:", settings.AuditStages.ToString()));
    }

    /// <summary>Adds the blank line and the Started, Finished, Elapsed and Result lines.</summary>
    /// <param name="lines">The report's lines.</param>
    /// <param name="startedUtc">When the run started, in UTC.</param>
    /// <param name="finishedUtc">When the run finished, in UTC.</param>
    /// <param name="result">How the run ended.</param>
    /// <param name="failureMessage">The failure message, used only for a failed run.</param>
    public static void AddTimingLines(List<string> lines, DateTime startedUtc, DateTime finishedUtc, AuditResultKinds result, string? failureMessage)
    {
        lines.Add(string.Empty);
        lines.Add(Labelled(label: "Started:", Timestamp(startedUtc.ToLocalTime())));
        lines.Add(Labelled(label: "Finished:", Timestamp(finishedUtc.ToLocalTime())));
        lines.Add(Labelled(label: "Elapsed:", Elapsed(finishedUtc - startedUtc)));
        lines.Add(Labelled(label: "Result:", ResultText(result, failureMessage)));
    }

    /// <summary>Formats a labelled value: the label padded to the label width, then the value on one line.</summary>
    /// <param name="label">The label, with its colon.</param>
    /// <param name="value">The value.</param>
    /// <returns>The line, without trailing white space.</returns>
    public static string Labelled(string label, string value) => (label.PadRight(LabelWidth) + Flatten(value)).TrimEnd();

    /// <summary>Formats a line that continues the previous labelled value: the label's width in spaces, then the value on one line.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The line, without trailing white space.</returns>
    public static string Continued(string value) => (new string(c: ' ', LabelWidth) + Flatten(value)).TrimEnd();

    /// <summary>Formats a count with thousands separators, in the invariant culture.</summary>
    /// <param name="value">The count.</param>
    /// <returns>The text, such as <c>48,207</c>.</returns>
    public static string Count(long value) => value.ToString(format: "N0", CultureInfo.InvariantCulture);

    /// <summary>Formats a local time stamp.</summary>
    /// <param name="value">The local time.</param>
    /// <returns>The text, such as <c>2026-11-08 14:02:11</c>.</returns>
    public static string Timestamp(DateTime value) => value.ToString(format: "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>Formats an elapsed time as <c>HH:mm:ss</c>, with the hours counting past 24 and a negative time as zero.</summary>
    /// <param name="elapsed">The elapsed time.</param>
    /// <returns>The text.</returns>
    public static string Elapsed(TimeSpan elapsed)
    {
        TimeSpan clamped = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        long     hours   = (long)clamped.TotalHours;

        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{clamped.Minutes:00}:{clamped.Seconds:00}");
    }

    /// <summary>Keeps a value on one line, so a multi-line system or SQL message cannot break the layout.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with each line break replaced by a space.</returns>
    public static string Flatten(string text)
        => text.Replace(oldValue: "\r\n", newValue: " ")
               .Replace(oldChar: '\n', newChar: ' ')
               .Replace(oldChar: '\r', newChar: ' ');

    private static string ResultText(AuditResultKinds result, string? failureMessage)
        => result switch
        {
            AuditResultKinds.Completed     => "Completed",
            AuditResultKinds.StoppedByUser => "Stopped by user",
            var _                          => $"Failed — {Flatten(failureMessage ?? string.Empty)}".TrimEnd()
        };
}
