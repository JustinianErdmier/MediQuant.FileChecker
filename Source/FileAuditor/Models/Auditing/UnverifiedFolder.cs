namespace FileAuditor.Models.Auditing;

/// <summary>A folder the folder walker could not list; its contents were not checked.</summary>
/// <param name="Path">The folder's full path, ending in <c>\</c>.</param>
/// <param name="Reason">The reason, one of the Unverified members of <see cref="CheckOutcome" />.</param>
/// <param name="ReasonText">A short reason; set only for <see cref="CheckOutcome.UnverifiedOther" />.</param>
/// <param name="SystemMessage">The system's message for the failure; set only for <see cref="CheckOutcome.UnverifiedOther" />.</param>
public sealed record UnverifiedFolder(string Path, CheckOutcome Reason, string? ReasonText, string? SystemMessage);
