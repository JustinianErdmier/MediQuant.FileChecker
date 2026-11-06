namespace FileAuditor.Services.Dialogues;

/// <summary>Shows the application's modal message dialogues, owned by the main window and titled <c>File Auditor</c>.</summary>
/// <remarks>
///     When there is no owner window the dialogues silently do nothing and report <see langword="false" />. That happens only in the XAML designer, where the parameterless view
///     model is built with no main window.
/// </remarks>
/// <param name="ownerProvider">Supplies the owner window; evaluated each time a dialogue opens.</param>
public sealed class DialogueService(Func<Window?> ownerProvider)
{
    /// <summary>Shows an error with a single OK button.</summary>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the dialogue closes.</returns>
    public async Task ShowErrorAsync(string message) => await ShowAsync(new MessageDialogueViewModel(message, DialogueIcon.Error, PrimaryText: "OK", SecondaryText: null));

    /// <summary>Shows a warning with an action button and Cancel.</summary>
    /// <param name="message">The message.</param>
    /// <param name="confirmText">The action button's text; an underscore marks its access key.</param>
    /// <returns><see langword="true" /> when the user chose the action; <see langword="false" /> for Cancel or closing the dialogue.</returns>
    public Task<bool> ConfirmAsync(string message, string confirmText)
        => ShowAsync(new MessageDialogueViewModel(message, DialogueIcon.Warning, confirmText, SecondaryText: "Cancel"));

    /// <summary>Asks a Yes/No question.</summary>
    /// <param name="message">The question.</param>
    /// <returns><see langword="true" /> for Yes; <see langword="false" /> for No or closing the dialogue.</returns>
    public Task<bool> AskYesNoAsync(string message) => ShowAsync(new MessageDialogueViewModel(message, DialogueIcon.Question, PrimaryText: "_Yes", SecondaryText: "_No"));

    private async Task<bool> ShowAsync(MessageDialogueViewModel viewModel)
    {
        Window? owner = ownerProvider();

        if (owner is null)
        {
            return false;
        }

        return await new MessageDialogueWindow(viewModel).ShowDialog<bool>(owner);
    }
}
