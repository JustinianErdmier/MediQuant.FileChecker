namespace FileAuditor.Common.Constants.Settings;

/// <summary>The stage of the audit being performed.</summary>
public enum AuditStages
{
    /// <summary>An interim audit, run before the project is complete.</summary>
    Interim,

    /// <summary>The final audit.</summary>
    Final
}
