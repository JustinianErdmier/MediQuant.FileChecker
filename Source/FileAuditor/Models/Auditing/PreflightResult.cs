namespace FileAuditor.Models.Auditing;

/// <summary>What the pre-flight checks hand to the run when it may start.</summary>
/// <param name="ReportPath">Where the report is written.</param>
/// <param name="PathColumnType">
///     The path column's type as pre-flight read it for the varchar warning (the lookup's own result, an error included), so the run need not connect for it again;
///     <see langword="null" /> when pre-flight did not read it (the warning already answered for this column) or the read did not finish (cancelled, or an exception).
/// </param>
public sealed record PreflightResult(ReportPath ReportPath, ErrorOr<(string TypeName, short MaxLength)?>? PathColumnType);
