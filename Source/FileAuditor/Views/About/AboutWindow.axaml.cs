using FileAuditor.ViewModels.About;

namespace FileAuditor.Views.About;

/// <summary>The modal About box.</summary>
public partial class AboutWindow : ClassicWindow
{
    /// <summary>Initialises a new instance of the <see cref="AboutWindow" /> class.</summary>
    public AboutWindow()
    {
        InitializeComponent();

        DataContext = new AboutWindowViewModel();
    }

    private void OnOkClick(object? sender, RoutedEventArgs e) => Close();
}
