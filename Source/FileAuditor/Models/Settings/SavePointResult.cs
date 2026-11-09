namespace FileAuditor.Models.Settings;

/// <summary>The outcome of a save point: whether the operation that triggered it may proceed, and whether the save checkbox should now be ticked.</summary>
/// <param name="Proceed"><see langword="true" /> when the triggering operation may go ahead; <see langword="false" /> when the user cancelled.</param>
/// <param name="TickCheckbox"><see langword="true" /> when the user answered Yes to the save prompt, so the checkbox should be ticked.</param>
public sealed record SavePointResult(bool Proceed, bool TickCheckbox)
{
    /// <summary>Gets the result for "the user cancelled; back out".</summary>
    public static SavePointResult Cancel { get; } = new(Proceed: false, TickCheckbox: false);

    /// <summary>Gets the result for "carry on, leave the checkbox alone".</summary>
    public static SavePointResult Go { get; } = new(Proceed: true, TickCheckbox: false);

    /// <summary>Gets the result for "carry on, and tick the checkbox".</summary>
    public static SavePointResult GoAndTick { get; } = new(Proceed: true, TickCheckbox: true);
}
