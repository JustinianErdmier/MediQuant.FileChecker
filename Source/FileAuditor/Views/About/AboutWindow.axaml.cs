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

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
