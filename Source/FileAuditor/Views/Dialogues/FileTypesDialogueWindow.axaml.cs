using JetBrains.Annotations;

namespace FileAuditor.Views.Dialogues;

/// <summary>A modal Win9x editor for a list of file types, working like the Windows "Edit environment variable" editor; <see cref="Result" /> holds the accepted entries.</summary>
public partial class FileTypesDialogueWindow : ClassicWindow
{
    /// <summary>Initialises a new instance for the XAML designer.</summary>
    [ UsedImplicitly ]
    public FileTypesDialogueWindow()
        : this(new FileTypesDialogueViewModel())
    { }

    /// <summary>Initialises a new instance showing the given content.</summary>
    /// <param name="viewModel">The content to show.</param>
    public FileTypesDialogueWindow(FileTypesDialogueViewModel viewModel)
    {
        InitializeComponent();

        Opened += (_, _) => this.HideMaximiseButton();

        DataContext = viewModel;

        // A line gets the focus when it is clicked into, and that selects it, as the list's own click handling does not reach a text box.
        EntriesList.AddHandler(GotFocusEvent, OnEntryGotFocus, RoutingStrategies.Bubble);
    }

    /// <summary>Gets the accepted, normalised entries, or <see langword="null" /> when the dialogue was cancelled.</summary>
    public string[]? Result { get; private set; }

    /// <summary>Gets the content of the dialogue, which is its data context.</summary>
    private FileTypesDialogueViewModel ViewModel => (FileTypesDialogueViewModel)DataContext!;

    /// <summary>Selects a line when it is clicked into.</summary>
    /// <param name="sender">The list the focus change bubbled up to.</param>
    /// <param name="eventArgs">The event data, whose source is the control that took the focus.</param>
    private void OnEntryGotFocus(object? sender, RoutedEventArgs eventArgs)
    {
        if (eventArgs.Source is TextBox { DataContext: FileTypeEntryItem item })
        {
            ViewModel.SelectedEntry = item;
        }
    }

    /// <summary>Adds an empty line and puts the cursor in it when New is clicked.</summary>
    /// <param name="sender">The New button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnNewClick(object? sender, RoutedEventArgs eventArgs) => FocusEntry(ViewModel.AddEntry());

    /// <summary>Puts the cursor in the selected line when Edit is clicked.</summary>
    /// <param name="sender">The Edit button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnEditClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (ViewModel.SelectedEntry is { } item)
        {
            FocusEntry(item);
        }
    }

    /// <summary>Scrolls a line into view and puts the cursor in it with its text selected.</summary>
    /// <param name="item">The line to focus.</param>
    private void FocusEntry(FileTypeEntryItem item)
    {
        EntriesList.ScrollIntoView(item);

        // The row's container exists only after the layout pass that follows the change.
        Dispatcher.UIThread.Post(() =>
                                 {
                                     TextBox? box = EntriesList.ContainerFromItem(item)
                                                               ?.GetVisualDescendants()
                                                               .OfType<TextBox>()
                                                               .FirstOrDefault();

                                     box?.Focus();
                                     box?.SelectAll();
                                 },
                                 DispatcherPriority.Loaded);
    }

    /// <summary>
    ///     Validates the entries when OK is clicked. A valid list becomes <see cref="Result" /> and closes the dialogue; an invalid entry is explained and the dialogue stays open.
    /// </summary>
    /// <param name="sender">The OK button.</param>
    /// <param name="eventArgs">The event data.</param>
    private async void OnOkClick(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            ErrorOr<string[]> parsed = ViewModel.Validate();

            if (parsed.IsError)
            {
                // The dialogue stays open, so the entry can be corrected.
                MessageDialogueViewModel message = new(parsed.FirstError.Description, DialogueIcons.Error, PrimaryText: "OK", SecondaryText: null);

                await new MessageDialogueWindow(message).ShowDialog(this);

                return;
            }

            Result = parsed.Value;

            Close();
        }
        catch (Exception exception)
        {
            UnexpectedErrorHandler.ReportRecoverable(exception);
        }
    }

    /// <summary>Closes the dialogue without a result when Cancel is clicked.</summary>
    /// <param name="sender">The Cancel button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnCancelClick(object? sender, RoutedEventArgs eventArgs) => Close();
}
