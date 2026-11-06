namespace FileAuditor.Models.Settings;

/// <summary>The stage of the audit being performed.</summary>
public enum AuditStage
{
    /// <summary>An interim audit, run before the project is complete.</summary>
    Interim,

    /// <summary>The final audit.</summary>
    Final
}
