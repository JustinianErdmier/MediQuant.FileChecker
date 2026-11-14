namespace FileAuditor.Models.Reporting;

/// <summary>Everything the report builder needs to write the plain-text report.</summary>
/// <param name="Settings">The settings the run used.</param>
/// <param name="StartedUtc">When the run started, in UTC.</param>
/// <param name="FinishedUtc">When the run finished, in UTC.</param>
/// <param name="Result">How the run ended.</param>
/// <param name="FailureMessage">The failure message; used only when <paramref name="Result" /> is <see cref="AuditResultKinds.Failed" />.</param>
/// <param name="Counts">How the table's rows divided into distinct real paths, duplicates, skip markers and empty paths, as read by the time the run ended.</param>
/// <param name="FoundCount">How many files were found.</param>
/// <param name="Problems">
///     The Missing and Unverified results only, so a very large run need not keep every Found row in memory. Files checked = <paramref name="FoundCount" /> plus
///     the number of problems.
/// </param>
/// <param name="EmptyPathIds">
///     The IDs of the rows whose path is empty that were read before the run ended (for a stopped or failed run, those read so far); <see langword="null" /> when the audit
///     has no ID column, which the report notes in place of the IDs.
/// </param>
public sealed record AuditReport(AppSettings                Settings,
                                 DateTime                   StartedUtc,
                                 DateTime                   FinishedUtc,
                                 AuditResultKinds           Result,
                                 string?                    FailureMessage,
                                 RowCounts                  Counts,
                                 long                       FoundCount,
                                 IReadOnlyList<CheckResult> Problems,
                                 IReadOnlyList<string>?     EmptyPathIds);
