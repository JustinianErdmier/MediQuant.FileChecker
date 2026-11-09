using JetBrains.Annotations;

namespace FileAuditor.Views.Dialogues;

/// <summary>A modal Win9x message dialogue; records which button closed it in <see cref="Choice" />.</summary>
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
        Choice = viewModel.HasTertiary ? DialogueChoices.Tertiary : viewModel.HasSecondary ? DialogueChoices.Secondary : DialogueChoices.Primary;
    }

    /// <summary>Gets the button that closed the dialogue; the last visible button when closed with the title-bar close box.</summary>
    public DialogueChoices Choice { get; private set; }

    /// <summary>Closes the dialogue with the primary button as the choice.</summary>
    /// <param name="sender">The primary button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnPrimaryClick(object? sender, RoutedEventArgs eventArgs) => CloseWith(DialogueChoices.Primary);

    /// <summary>Closes the dialogue with the second button as the choice.</summary>
    /// <param name="sender">The second button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnSecondaryClick(object? sender, RoutedEventArgs eventArgs) => CloseWith(DialogueChoices.Secondary);

    /// <summary>Closes the dialogue with the third button as the choice.</summary>
    /// <param name="sender">The third button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnTertiaryClick(object? sender, RoutedEventArgs eventArgs) => CloseWith(DialogueChoices.Tertiary);

    /// <summary>Records the button that was chosen and closes the dialogue.</summary>
    /// <param name="choice">The button that was chosen.</param>
    private void CloseWith(DialogueChoices choice)
    {
        Choice = choice;

        Close();
    }
}
