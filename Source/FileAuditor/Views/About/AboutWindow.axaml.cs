namespace FileAuditor.Views.About;

/// <summary>The modal About box.</summary>
public partial class AboutWindow : ClassicWindow
{
    /// <summary>Initialises a new instance of the <see cref="AboutWindow" /> class.</summary>
    public AboutWindow()
    {
        InitializeComponent();

        Opened += (_, _) => this.HideMaximiseButton();

        DataContext = new AboutWindowViewModel();
    }

    /// <summary>Closes the box when OK is clicked.</summary>
    /// <param name="sender">The OK button.</param>
    /// <param name="eventArgs">The event data.</param>
    private void OnOkClick(object? sender, RoutedEventArgs eventArgs) => Close();
}
