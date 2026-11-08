namespace FileAuditor.ViewModels.Dialogues;

/// <summary>One line of the file types editor: an entry as typed, which is validated when the dialogue is accepted.</summary>
public sealed partial class FileTypeEntryItem : ObservableObject
{
    /// <summary>Initialises a new instance.</summary>
    /// <param name="text">The entry's text.</param>
    public FileTypeEntryItem(string text) => Text = text;

    /// <summary>Gets or sets the entry's text.</summary>
    [ ObservableProperty ]
    public partial string Text { get; set; }
}
