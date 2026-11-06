namespace FileAuditor.ViewModels.Dialogues;

/// <summary>
///     The content of a message dialogue: a message, an icon, an action button and an optional escape button.
/// </summary>
/// <param name="Message">The message text; may span several lines.</param>
/// <param name="Icon">The icon to show.</param>
/// <param name="PrimaryText">The action button's text; an underscore marks its access key.</param>
/// <param name="SecondaryText">The escape button's text, or <see langword="null" /> for a dialogue with a single button.</param>
public sealed record MessageDialogueViewModel(string Message, DialogueIcon Icon, string PrimaryText, string? SecondaryText)
{
    /// <summary>Gets a value indicating whether to show the error icon.</summary>
    public bool IsError => Icon == DialogueIcon.Error;

    /// <summary>Gets a value indicating whether to show the warning icon.</summary>
    public bool IsWarning => Icon == DialogueIcon.Warning;

    /// <summary>Gets a value indicating whether to show the question icon.</summary>
    public bool IsQuestion => Icon == DialogueIcon.Question;

    /// <summary>Gets a value indicating whether the escape button is shown.</summary>
    public bool HasSecondary => SecondaryText is not null;
}
