namespace FileAuditor.Models.Reporting;

/// <summary>Everything the report builder needs to write the plain-text unindexed-files report.</summary>
/// <param name="Settings">The settings the run used.</param>
/// <param name="StartedUtc">When the run started, in UTC.</param>
/// <param name="FinishedUtc">When the run finished, in UTC.</param>
/// <param name="Result">How the run ended: completed, stopped by the user, or failed.</param>
/// <param name="Unindexed">The files that are not in the index.</param>
/// <param name="Unverified">The folders that could not be listed and the links that were not followed.</param>
/// <param name="TotalFiles">The number of files checked: <paramref name="Indexed" /> plus the unindexed files. Excluded files are not part of it.</param>
/// <param name="Indexed">The number of files found that are in the index.</param>
/// <param name="Excluded">The number of files skipped because no file type matches them or an exclude entry does.</param>
/// <param name="IndexPaths">The number of distinct index paths loaded to compare against, after trimming and ignoring letter case.</param>
/// <param name="FailureMessage">The failure message; used only when <paramref name="Result" /> is <see cref="AuditResultKinds.Failed" />.</param>
public sealed record UnindexedReport(AppSettings                     Settings,
                                     DateTime                        StartedUtc,
                                     DateTime                        FinishedUtc,
                                     AuditResultKinds                Result,
                                     IReadOnlyList<ScannedFile>      Unindexed,
                                     IReadOnlyList<UnverifiedFolder> Unverified,
                                     long                            TotalFiles,
                                     long                            Indexed,
                                     long                            Excluded,
                                     long                            IndexPaths,
                                     string?                         FailureMessage = null);
