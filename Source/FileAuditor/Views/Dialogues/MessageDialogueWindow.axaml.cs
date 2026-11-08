using JetBrains.Annotations;

namespace FileAuditor.Views.Dialogues;

/// <summary>A modal Win9x message dialogue; records which button closed it in <see cref="Choices" />.</summary>
public partial class MessageDialogueWindow : ClassicWindow
{
    /// <summary>Initialises a new instance for the XAML designer.</summary>
    [ UsedImplicitly ]
    public MessageDialogueWindow()
        : this(new MessageDialogueViewModel(Message: "Message", DialogueIcons.Warning, PrimaryText: "OK", SecondaryText: "Cancel"))
    { }

    /// <summary>Initialises a new instance showing the given content.</summary>
    /// <param name="viewModel">The content to show.</param>
    public MessageDialogueWindow(MessageDialogueViewModel viewModel)
    {
        InitializeComponent();

        Opened += (_, _) => this.HideMaximiseButton();

        DataContext = viewModel;

        // Closing with the title-bar close box counts as the last visible button.
        Choices = viewModel.HasTertiary ? DialogueChoices.Tertiary : viewModel.HasSecondary ? DialogueChoices.Secondary : DialogueChoices.Primary;
    }

    /// <summary>Gets the button that closed the dialogue; the last visible button when closed with the title-bar close box.</summary>
    public DialogueChoices Choices { get; private set; }

    private void OnPrimaryClick(object? sender, RoutedEventArgs eventArgs) => CloseWith(DialogueChoices.Primary);

    private void OnSecondaryClick(object? sender, RoutedEventArgs eventArgs) => CloseWith(DialogueChoices.Secondary);

    private void OnTertiaryClick(object? sender, RoutedEventArgs eventArgs) => CloseWith(DialogueChoices.Tertiary);

    private void CloseWith(DialogueChoices choices)
    {
        Choices = choices;

        Close();
    }
}
