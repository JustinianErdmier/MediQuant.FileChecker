namespace FileAuditor.Services.Dialogues;

/// <summary>A file the user chose in the Open Project picker.</summary>
/// <param name="Name">The file's name, for messages.</param>
/// <param name="LocalPath">The file's local or network path, or <see langword="null" /> when it is not on a local or network drive and so cannot be opened.</param>
public sealed record ProjectFilePick(string Name, string? LocalPath);
