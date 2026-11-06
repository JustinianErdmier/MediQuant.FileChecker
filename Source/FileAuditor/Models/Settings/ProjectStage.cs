namespace FileAuditor.Models.Settings;

/// <summary>
///     The stage of the project whose files are being audited.
/// </summary>
public enum ProjectStage
{
    /// <summary>The project is at its sample stage.</summary>
    Sample,

    /// <summary>The project is at its final stage.</summary>
    Final
}
