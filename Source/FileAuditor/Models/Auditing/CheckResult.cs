namespace FileAuditor.Models.Auditing;

/// <summary>The result of checking one index row.</summary>
/// <param name="Row">The row that was checked.</param>
/// <param name="Outcomes">The outcome the row's path landed in.</param>
/// <param name="Reason">A short reason; set only for <see cref="CheckOutcomes.UnverifiedOther" />.</param>
/// <param name="SystemMessage">The system's message for the failure; set only for <see cref="CheckOutcomes.UnverifiedOther" />.</param>
public sealed record CheckResult(IndexRow Row, CheckOutcomes Outcomes, string? Reason, string? SystemMessage);
