namespace FileAuditor.ViewModels.Dialogues;

/// <summary>The content of the file types editor: one editable entry per line, with New, Delete, Clear and (for File types only) Reset.</summary>
public sealed partial class FileTypesDialogueViewModel : ObservableObject
{
    /// <summary>Initialises a new instance with placeholder content, for the XAML designer.</summary>
    public FileTypesDialogueViewModel()
        : this(title: "Edit File Types", FileTypeList.Defaults, canReset: true)
    { }

    /// <summary>Initialises a new instance.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="entries">The entries to start with.</param>
    /// <param name="canReset">Whether the dialogue offers Reset, which restores the built-in file types.</param>
    public FileTypesDialogueViewModel(string title, IEnumerable<string> entries, bool canReset)
    {
        Title    = title;
        CanReset = canReset;

        Entries.CollectionChanged += (_, _) => ClearCommand.NotifyCanExecuteChanged();

        foreach (string entry in entries)
        {
            Entries.Add(new FileTypeEntryItem(entry));
        }
    }

    /// <summary>Gets a value indicating whether Reset is offered.</summary>
    public bool CanReset { get; }

    /// <summary>Gets the entries, one per line.</summary>
    public ObservableCollection<FileTypeEntryItem> Entries { get; } = [];

    /// <summary>Gets or sets the selected line.</summary>
    [ ObservableProperty ]
    [ NotifyCanExecuteChangedFor(nameof(DeleteCommand)) ]
    public partial FileTypeEntryItem? SelectedEntry { get; set; }

    /// <summary>Gets the window title.</summary>
    public string Title { get; }

    /// <summary>Appends an empty line and selects it.</summary>
    /// <returns>The new line.</returns>
    public FileTypeEntryItem AddEntry()
    {
        FileTypeEntryItem item = new(text: "");

        Entries.Add(item);

        SelectedEntry = item;

        return item;
    }

    /// <summary>Validates and normalises the entries. Each line is one entry, so a line holding a space or a comma is not valid.</summary>
    /// <returns>The normalised entries, or the first entry that is not valid.</returns>
    public ErrorOr<string[]> Validate()
    {
        List<string> entries = [];

        foreach (FileTypeEntryItem entry in Entries)
        {
            string text = entry.Text.Trim();

            if (text is [])
            {
                continue;
            }

            if (text.Any(character => char.IsWhiteSpace(character) || character is ','))
            {
                return FileTypeList.InvalidEntry(text);
            }

            entries.Add(text);
        }

        return FileTypeList.Parse(string.Join(separator: ' ', entries));
    }

    /// <summary>Determines whether a line is selected, so Delete can run.</summary>
    /// <returns><see langword="true" /> when there is a selected line.</returns>
    private bool CanDelete() => SelectedEntry is not null;

    /// <summary>Removes the selected line and selects the one that takes its place.</summary>
    [ RelayCommand(CanExecute = nameof(CanDelete)) ]
    private void Delete()
    {
        if (SelectedEntry is not { } item)
        {
            return;
        }

        int index = Entries.IndexOf(item);

        Entries.Remove(item);

        SelectedEntry = Entries is [] ? null : Entries[Math.Min(index, Entries.Count - 1)];
    }

    /// <summary>Determines whether there are any lines, so Clear can run.</summary>
    /// <returns><see langword="true" /> when the list is not empty.</returns>
    private bool CanClear() => Entries.Count > 0;

    /// <summary>Removes every line.</summary>
    [ RelayCommand(CanExecute = nameof(CanClear)) ]
    private void Clear()
    {
        Entries.Clear();

        SelectedEntry = null;
    }

    /// <summary>Replaces every line with the built-in file types.</summary>
    [ RelayCommand ]
    private void Reset()
    {
        Entries.Clear();

        foreach (string entry in FileTypeList.Defaults)
        {
            Entries.Add(new FileTypeEntryItem(entry));
        }
    }
}
