namespace FileAuditor.Models.Reporting;

/// <summary>Everything the report builder needs to write the plain-text report (SDD §7).</summary>
/// <param name="Settings">The settings the run used.</param>
/// <param name="StartedUtc">When the run started, in UTC.</param>
/// <param name="FinishedUtc">When the run finished, in UTC.</param>
/// <param name="Result">How the run ended.</param>
/// <param name="FailureMessage">The failure message; used only when <paramref name="Result" /> is <see cref="AuditResultKind.Failed" />.</param>
/// <param name="TotalFiles">The table's row count.</param>
/// <param name="FoundCount">How many files were found.</param>
/// <param name="Problems">
///     The Missing and Unverified results only, so a very large run need not keep every Found row in memory. Files checked = <paramref name="FoundCount" /> plus
///     the number of problems.
/// </param>
public sealed record AuditReport(AppSettings                Settings,
                                 DateTime                   StartedUtc,
                                 DateTime                   FinishedUtc,
                                 AuditResultKind            Result,
                                 string?                    FailureMessage,
                                 long                       TotalFiles,
                                 long                       FoundCount,
                                 IReadOnlyList<CheckResult> Problems);
