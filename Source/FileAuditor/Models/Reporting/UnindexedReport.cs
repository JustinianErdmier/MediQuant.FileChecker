namespace FileAuditor.Models.Reporting;

/// <summary>Everything the report builder needs to write the plain-text unindexed-files report (milestone 4 spec §3.7).</summary>
/// <param name="Settings">The settings the run used.</param>
/// <param name="StartedUtc">When the run started, in UTC.</param>
/// <param name="FinishedUtc">When the run finished, in UTC.</param>
/// <param name="Kind">How the run ended: completed, stopped by the user, or failed.</param>
/// <param name="Unindexed">The files that are not in the index.</param>
/// <param name="Unverified">The folders that could not be listed.</param>
/// <param name="TotalFiles">The number of files found.</param>
/// <param name="Indexed">The number of files found that are in the index.</param>
/// <param name="NotChecked">The number of files reached through links that were found but not checked against the index, because a stop or a SQL failure came first.</param>
/// <param name="FailureMessage">The failure message; used only when <paramref name="Kind" /> is <see cref="AuditResultKind.Failed" />.</param>
public sealed record UnindexedReport(AppSettings                     Settings,
                                     DateTime                        StartedUtc,
                                     DateTime                        FinishedUtc,
                                     AuditResultKind                 Kind,
                                     IReadOnlyList<ScannedFile>      Unindexed,
                                     IReadOnlyList<UnverifiedFolder> Unverified,
                                     long                            TotalFiles,
                                     long                            Indexed,
                                     long                            NotChecked    = 0,
                                     string?                         FailureMessage = null);
