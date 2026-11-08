namespace FileAuditor.Services.Checking;

/// <summary>An outcome derived from a file-system exception, with the reason and system message that accompany <see cref="CheckOutcomes.UnverifiedOther" />.</summary>
/// <param name="Outcomes">The mapped outcome.</param>
/// <param name="Reason">A short reason; set only for <see cref="CheckOutcomes.UnverifiedOther" />.</param>
/// <param name="SystemMessage">The exception message; set only for <see cref="CheckOutcomes.UnverifiedOther" />.</param>
internal readonly record struct MappedOutcome(CheckOutcomes Outcomes, string? Reason = null, string? SystemMessage = null);
