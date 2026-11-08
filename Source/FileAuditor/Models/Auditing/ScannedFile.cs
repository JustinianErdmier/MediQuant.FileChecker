namespace FileAuditor.Models.Auditing;

/// <summary>A file the folder walker found that belongs to one of the chosen file types.</summary>
/// <param name="Path">The file's full path, as it was reached (a file under a link is reported by the link's path).</param>
/// <param name="InputFolder">The input folder the file is reported under: the deepest one whose scope covers it, as the user entered it.</param>
/// <param name="Type">The File types entry the file belongs to.</param>
/// <param name="CanonicalPath">
///     The file's path with the link it was reached through replaced by that link's resolved target, so the index may hold either path; <see langword="null" />
///     when <paramref name="Path" /> is already canonical (the file was not reached through a link).
/// </param>
public sealed record ScannedFile(string Path, string InputFolder, FileTypePattern Type, string? CanonicalPath = null);
