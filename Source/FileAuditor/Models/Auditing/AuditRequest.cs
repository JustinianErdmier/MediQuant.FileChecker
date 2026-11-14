namespace FileAuditor.Models.Auditing;

/// <summary>What an audit needs to run: the validated settings, the resolved report path and, when pre-flight read it, the path column's type.</summary>
/// <param name="Settings">The settings the audit uses.</param>
/// <param name="ReportPath">Where the report is written.</param>
/// <param name="PathColumnType">
///     The path column's type as pre-flight read it (an error included), used instead of reading it again, so an unreachable server is waited for once;
///     <see langword="null" /> to have the run read it.
/// </param>
public sealed record AuditRequest(AppSettings Settings, ReportPath ReportPath, ErrorOr<(string TypeName, short MaxLength)?>? PathColumnType = null);
