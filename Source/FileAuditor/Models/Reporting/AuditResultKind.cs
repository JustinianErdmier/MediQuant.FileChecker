namespace FileAuditor.Models.Reporting;

/// <summary>How an audit run ended.</summary>
public enum AuditResultKind
{
    /// <summary>Every row was read and checked.</summary>
    Completed,

    /// <summary>The user stopped the run before every row was checked.</summary>
    StoppedByUser,

    /// <summary>The run failed part-way, for example because SQL Server dropped the connection.</summary>
    Failed
}
