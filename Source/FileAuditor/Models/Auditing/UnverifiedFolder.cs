namespace FileAuditor.Models.Auditing;

/// <summary>A folder the folder walker could not list, or a folder or file it met that is a link and did not follow; the contents or file were not checked.</summary>
/// <param name="Path">The full path: a folder ends in <c>\</c>, a file link does not.</param>
/// <param name="Reason">The reason, one of the Unverified members of <see cref="CheckOutcome" />.</param>
/// <param name="ReasonText">A short reason; set only for <see cref="CheckOutcome.UnverifiedOther" />.</param>
/// <param name="SystemMessage">The system's message for the failure; set only for <see cref="CheckOutcome.UnverifiedOther" />.</param>
public sealed record UnverifiedFolder(string Path, CheckOutcome Reason, string? ReasonText, string? SystemMessage);
