namespace FileAuditor.ViewModels.Dialogues;

/// <summary>The content of a message dialogue: a message, an icon, an action button and up to two further buttons.</summary>
/// <param name="Message">The message text; may span several lines.</param>
/// <param name="Icons">The icon to show.</param>
/// <param name="PrimaryText">The action button's text; an underscore marks its access key.</param>
/// <param name="SecondaryText">The second button's text, or <see langword="null" /> for a dialogue with a single button.</param>
/// <param name="TertiaryText">The third button's text, or <see langword="null" /> for a dialogue with at most two buttons.</param>
public sealed record MessageDialogueViewModel(string Message, DialogueIcons Icons, string PrimaryText, string? SecondaryText, string? TertiaryText = null)
{
    /// <summary>Initialises a new instance with placeholder content, for the XAML designer.</summary>
    public MessageDialogueViewModel()
        : this(Message: "The report folder does not exist.", DialogueIcons.Warning, PrimaryText: "_Create Folder", SecondaryText: "Cancel")
    { }

    /// <summary>Gets a value indicating whether the second button is shown.</summary>
    public bool HasSecondary => SecondaryText is not null;

    /// <summary>Gets a value indicating whether the third button is shown.</summary>
    public bool HasTertiary => TertiaryText is not null;

    /// <summary>Gets a value indicating whether to show the error icon.</summary>
    public bool IsError => Icons is DialogueIcons.Error;

    /// <summary>Gets a value indicating whether to show the question icon.</summary>
    public bool IsQuestion => Icons is DialogueIcons.Question;

    /// <summary>Gets a value indicating whether to show the warning icon.</summary>
    public bool IsWarning => Icons is DialogueIcons.Warning;

    /// <summary>Gets a value indicating whether the primary button is the only visible one, and so the escape button.</summary>
    public bool PrimaryIsCancel => !HasSecondary;

    /// <summary>Gets a value indicating whether the second button is the last visible one, and so the escape button.</summary>
    public bool SecondaryIsCancel => HasSecondary && !HasTertiary;
}
