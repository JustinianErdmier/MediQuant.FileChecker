namespace FileAuditor.Models.Reporting;

/// <summary>Everything the report builder needs to write the plain-text report.</summary>
/// <param name="Settings">The settings the run used.</param>
/// <param name="StartedUtc">When the run started, in UTC.</param>
/// <param name="FinishedUtc">When the run finished, in UTC.</param>
/// <param name="Result">How the run ended.</param>
/// <param name="FailureMessage">The failure message; used only when <paramref name="Result" /> is <see cref="AuditResultKinds.Failed" />.</param>
/// <param name="Counts">How the table's rows divide into empty paths, skip markers, distinct real paths and duplicates, as counted before checking.</param>
/// <param name="FoundCount">How many files were found.</param>
/// <param name="Problems">
///     The Missing and Unverified results only, so a very large run need not keep every Found row in memory. Files checked = <paramref name="FoundCount" /> plus
///     the number of problems.
/// </param>
/// <param name="EmptyPathIds">
///     The IDs of the rows whose path is empty; <see langword="null" /> when there are none or they could not be listed, in which case <paramref name="EmptyPathNote" />
///     says why. When <c>Counts.EmptyPaths</c> is above zero, exactly one of the two is not <see langword="null" />.
/// </param>
/// <param name="EmptyPathNote">The line that stands in for the empty-path IDs when they are not listed; <see langword="null" /> otherwise.</param>
public sealed record AuditReport(AppSettings                Settings,
                                 DateTime                   StartedUtc,
                                 DateTime                   FinishedUtc,
                                 AuditResultKinds           Result,
                                 string?                    FailureMessage,
                                 RowCounts                  Counts,
                                 long                       FoundCount,
                                 IReadOnlyList<CheckResult> Problems,
                                 IReadOnlyList<string>?     EmptyPathIds,
                                 string?                    EmptyPathNote);
