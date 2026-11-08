namespace FileAuditor.Services.Reporting;

/// <summary>Where the report is written: a folder and a file name, plus whether both are known yet.</summary>
/// <param name="Directory">The report folder; for a built path with no project folder it holds the <c>&lt;Project Folder&gt;</c> placeholder.</param>
/// <param name="FileName">The report file name.</param>
/// <param name="IsComplete">Whether every part of the path is filled in, so the report can be written.</param>
/// <param name="IsCustom">Whether the user supplied the location and file name rather than the built ones.</param>
public sealed record ReportPath(string Directory, string FileName, bool IsComplete, bool IsCustom)
{
    /// <summary>Gets the folder as shown in the Location box: a built folder always ends with a backslash, a custom one is shown as typed.</summary>
    public string DisplayDirectory
        => IsCustom || Directory.EndsWith(Path.DirectorySeparatorChar)
               ? Directory
               : Directory + Path.DirectorySeparatorChar;

    /// <summary>Gets the full path of the report file.</summary>
    public string FullPath => Path.Combine(Directory, FileName);
}
