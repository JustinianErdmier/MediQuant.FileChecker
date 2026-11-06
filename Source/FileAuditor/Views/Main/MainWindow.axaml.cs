using FileAuditor.ViewModels.Main;
using FileAuditor.Views.About;

namespace FileAuditor.Views.Main;

/// <summary>The File Auditor main window.</summary>
public partial class MainWindow : ClassicWindow
{
    /// <summary>Initialises a new instance of the <see cref="MainWindow" /> class.</summary>
    public MainWindow()
    {
        InitializeComponent();

        Opened += (_, _) => this.HideMaximiseButton();

        // The report may have been created or deleted while another program had focus.
        Activated += (_, _) => (DataContext as MainWindowViewModel)?.RefreshReportState();
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AboutRequested += async (_, _) => await new AboutWindow().ShowDialog(this);
            viewModel.ExitRequested += (_, _) => Close();
        }
    }
}
