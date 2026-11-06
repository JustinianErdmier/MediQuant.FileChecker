namespace FileAuditor.Views.Main;

/// <summary>The File Auditor main window.</summary>
public partial class MainWindow : ClassicWindow
{
    private bool _closeApproved;

    private bool _isAsking;

    /// <summary>Initialises a new instance of the <see cref="MainWindow" /> class.</summary>
    public MainWindow()
    {
        InitializeComponent();

        Opened += (_, _) => this.HideMaximiseButton();

        // The report may have been created or deleted while another program had focus.
        Activated += (_, _) => (DataContext as MainWindowViewModel)?.RefreshReportState();
    }

    /// <inheritdoc />
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_closeApproved
            || e.Cancel
            || DataContext is not MainWindowViewModel { IsRunning: true } viewModel)
        {
            return;
        }

        // The question is asynchronous, so cancel this close and repeat it once the user has answered.
        e.Cancel = true;

        if (!_isAsking)
        {
            _ = CloseWhenConfirmedAsync(viewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.AboutRequested += async (_, _) => await new AboutWindow().ShowDialog(this);
            viewModel.ExitRequested  += (_,       _) => Close();
        }
    }

    private async Task CloseWhenConfirmedAsync(MainWindowViewModel viewModel)
    {
        _isAsking = true;

        try
        {
            if (await viewModel.ConfirmCloseAsync())
            {
                _closeApproved = true;

                Close();
            }
        }
        finally
        {
            _isAsking = false;
        }
    }
}
