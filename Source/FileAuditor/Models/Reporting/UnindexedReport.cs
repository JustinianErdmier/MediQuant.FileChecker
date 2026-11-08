namespace FileAuditor.Models.Reporting;

/// <summary>Everything the report builder needs to write the plain-text unindexed-files report (milestone 4 spec §3.7).</summary>
/// <param name="Settings">The settings the run used.</param>
/// <param name="StartedUtc">When the run started, in UTC.</param>
/// <param name="FinishedUtc">When the run finished, in UTC.</param>
/// <param name="Kind">How the run ended: completed, or stopped by the user.</param>
/// <param name="Unindexed">The files that are not in the index.</param>
/// <param name="Unverified">The folders that could not be listed.</param>
/// <param name="TotalFiles">The number of files found.</param>
/// <param name="Indexed">The number of files found that are in the index.</param>
public sealed record UnindexedReport(AppSettings                     Settings,
                                     DateTime                        StartedUtc,
                                     DateTime                        FinishedUtc,
                                     AuditResultKind                 Kind,
                                     IReadOnlyList<ScannedFile>      Unindexed,
                                     IReadOnlyList<UnverifiedFolder> Unverified,
                                     long                            TotalFiles,
                                     long                            Indexed);
