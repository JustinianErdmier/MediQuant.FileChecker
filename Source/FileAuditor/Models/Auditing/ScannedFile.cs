namespace FileAuditor.Models.Auditing;

/// <summary>A file the folder walker found that belongs to one of the chosen file types.</summary>
/// <param name="Path">The file's full path, as the walk reached it.</param>
/// <param name="InputFolder">The input folder the file is reported under: the deepest one whose scope covers it, as the user entered it.</param>
/// <param name="Type">The File types entry the file belongs to.</param>
public sealed record ScannedFile(string Path, string InputFolder, FileTypePattern Type);
