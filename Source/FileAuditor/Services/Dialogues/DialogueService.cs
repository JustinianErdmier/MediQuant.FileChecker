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
    public async Task<bool> ConfirmAsync(string message, string confirmText)
        => await ShowAsync(new MessageDialogueViewModel(message, DialogueIcon.Warning, confirmText, SecondaryText: "Cancel")) == DialogueChoice.Primary;

    /// <summary>Asks a Yes/No question.</summary>
    /// <param name="message">The question.</param>
    /// <returns><see langword="true" /> for Yes; <see langword="false" /> for No or closing the dialogue.</returns>
    public async Task<bool> AskYesNoAsync(string message)
        => await ShowAsync(new MessageDialogueViewModel(message, DialogueIcon.Question, PrimaryText: "_Yes", SecondaryText: "_No")) == DialogueChoice.Primary;

    /// <summary>Shows a dialogue with two or three buttons.</summary>
    /// <param name="message">The message.</param>
    /// <param name="icon">The icon to show.</param>
    /// <param name="primaryText">The action button's text; an underscore marks its access key.</param>
    /// <param name="secondaryText">The second button's text.</param>
    /// <param name="tertiaryText">The third button's text, or <see langword="null" /> for a two-button dialogue.</param>
    /// <returns>The button chosen; the last visible button when the dialogue is closed with the title-bar close box.</returns>
    public Task<DialogueChoice> ChooseAsync(string message, DialogueIcon icon, string primaryText, string secondaryText, string? tertiaryText = null)
        => ShowAsync(new MessageDialogueViewModel(message, icon, primaryText, secondaryText, tertiaryText));

    /// <summary>Shows the file types editor.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="entries">The entries to start with.</param>
    /// <param name="canReset">Whether the editor offers Reset, which restores the built-in file types.</param>
    /// <returns>The accepted entries in normal form, or <see langword="null" /> when the editor was cancelled.</returns>
    public async Task<string[]?> EditFileTypesAsync(string title, IReadOnlyList<string> entries, bool canReset)
    {
        Window? owner = ownerProvider();

        if (owner is null)
        {
            return null;
        }

        FileTypesDialogueWindow window = new(new FileTypesDialogueViewModel(title, entries, canReset));

        await window.ShowDialog(owner);

        return window.Result;
    }

    /// <summary>Shows the Open Project file picker.</summary>
    /// <param name="startFolder">The folder to start in; ignored when it does not exist.</param>
    /// <returns>
    ///     <see langword="null" /> when the picker was cancelled; otherwise the chosen file, whose <see cref="ProjectFilePick.LocalPath" /> is <see langword="null" /> when it is not
    ///     a file on a local or network drive.
    /// </returns>
    public async Task<ProjectFilePick?> PickProjectFileAsync(string startFolder)
    {
        Window? owner = ownerProvider();

        if (owner is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title                  = "Open Project",
            AllowMultiple          = false,
            SuggestedStartLocation = await owner.StorageProvider.TryGetFolderFromPathAsync(startFolder),
            FileTypeFilter =
            [
                new FilePickerFileType(name: "File Auditor project (FileAuditorSettings.json)")
                {
                    Patterns =
                    [
                        "FileAuditorSettings.json"
                    ]
                },
                new FilePickerFileType(name: "JSON files (*.json)")
                {
                    Patterns =
                    [
                        "*.json"
                    ]
                }
            ]
        });

        return files.Count == 0
                   ? null
                   : new ProjectFilePick(files[index: 0].Name,
                                         files[index: 0]
                                             .TryGetLocalPath());
    }

    private async Task<DialogueChoice> ShowAsync(MessageDialogueViewModel viewModel)
    {
        Window? owner = ownerProvider();

        if (owner is null)
        {
            return DialogueChoice.Secondary;
        }

        MessageDialogueWindow window = new(viewModel);

        await window.ShowDialog(owner);

        return window.Choice;
    }
}
