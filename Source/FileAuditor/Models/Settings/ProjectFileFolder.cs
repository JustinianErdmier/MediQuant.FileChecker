namespace FileAuditor.Models.Settings;

/// <summary>A scan folder as it is written in a project file, where a damaged file can leave any value out.</summary>
/// <param name="Path">The folder's full path.</param>
/// <param name="IncludeSubfolders">Whether the folders beneath it are checked too.</param>
public sealed record ProjectFileFolder(string? Path, bool? IncludeSubfolders);
