namespace FileAuditor.Models.Auditing;

/// <summary>What an audit needs to run: the validated settings and the resolved report path.</summary>
/// <param name="Settings">The settings the audit uses.</param>
/// <param name="ReportPath">Where the report is written.</param>
public sealed record AuditRequest(AppSettings Settings, ReportPath ReportPath);
