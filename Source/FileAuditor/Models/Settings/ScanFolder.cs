namespace FileAuditor.Models.Settings;

/// <summary>A folder the unindexed-files audit checks.</summary>
/// <param name="Path">The folder's full path.</param>
/// <param name="IncludeSubfolders">Whether the folders beneath it are checked too.</param>
public sealed record ScanFolder(string Path, bool IncludeSubfolders = true);
