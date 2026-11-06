namespace FileAuditor.Services.Checking;

/// <summary>
///     An outcome derived from a file-system exception, with the reason and system message that accompany <see cref="CheckOutcome.UnverifiedOther" />.
/// </summary>
/// <param name="Outcome">The mapped outcome.</param>
/// <param name="Reason">A short reason; set only for <see cref="CheckOutcome.UnverifiedOther" />.</param>
/// <param name="SystemMessage">The exception message; set only for <see cref="CheckOutcome.UnverifiedOther" />.</param>
internal readonly record struct MappedOutcome(CheckOutcome Outcome, string? Reason = null, string? SystemMessage = null);
