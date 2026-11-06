namespace FileAuditor.Views.Dialogues;

/// <summary>A modal Win9x message dialogue; closes with <see langword="true" /> for the action button and <see langword="false" /> otherwise.</summary>
public partial class MessageDialogueWindow : ClassicWindow
{
    /// <summary>Initialises a new instance for the XAML designer.</summary>
    public MessageDialogueWindow()
        : this(new MessageDialogueViewModel(Message: "Message", DialogueIcon.Warning, PrimaryText: "OK", SecondaryText: "Cancel"))
    { }

    /// <summary>Initialises a new instance showing the given content.</summary>
    /// <param name="viewModel">The content to show.</param>
    public MessageDialogueWindow(MessageDialogueViewModel viewModel)
    {
        InitializeComponent();

        Opened += (_, _) => this.HideMaximiseButton();

        DataContext = viewModel;
    }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => Close(dialogResult: true);

    private void OnSecondaryClick(object? sender, RoutedEventArgs e) => Close(dialogResult: false);
}
