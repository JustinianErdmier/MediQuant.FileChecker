namespace FileAuditor.Views.Dialogues;

/// <summary>A modal Win9x message dialogue; records which button closed it in <see cref="Choice" />.</summary>
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

        // Closing with the title-bar close box counts as the last visible button.
        Choice = viewModel.HasTertiary ? DialogueChoice.Tertiary : viewModel.HasSecondary ? DialogueChoice.Secondary : DialogueChoice.Primary;
    }

    /// <summary>Gets the button that closed the dialogue; the last visible button when closed with the title-bar close box.</summary>
    public DialogueChoice Choice { get; private set; }

    private void OnPrimaryClick(object? sender, RoutedEventArgs e) => CloseWith(DialogueChoice.Primary);

    private void OnSecondaryClick(object? sender, RoutedEventArgs e) => CloseWith(DialogueChoice.Secondary);

    private void OnTertiaryClick(object? sender, RoutedEventArgs e) => CloseWith(DialogueChoice.Tertiary);

    private void CloseWith(DialogueChoice choice)
    {
        Choice = choice;

        Close();
    }
}
