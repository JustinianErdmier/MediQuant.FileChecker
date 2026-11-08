namespace FileAuditor.Models.Auditing;

/// <summary>The text of the Unindexed Files tab's File types and Exclude boxes, as typed, so the pre-flight can name the first entry that is not valid.</summary>
/// <param name="FileTypes">The File types box.</param>
/// <param name="Exclude">The Exclude box.</param>
public sealed record FileTypeText(string FileTypes, string Exclude);
