namespace FileAuditor.Views.Main;

/// <summary>The File Auditor main window.</summary>
public partial class MainWindow : ClassicWindow
{
    /// <summary>Whether the user has confirmed the close, so the next closing request goes through.</summary>
    private bool _closeApproved;

    /// <summary>Whether the close question is open, so a second closing request does not ask again.</summary>
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
    protected override void OnClosing(WindowClosingEventArgs eventArgs)
    {
        base.OnClosing(eventArgs);

        if (_closeApproved
            || eventArgs.Cancel
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        // The question is asynchronous, so cancel this close and repeat it once the user has answered.
        eventArgs.Cancel = true;

        if (!_isAsking)
        {
            _ = CloseWhenConfirmedAsync(viewModel);
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs eventArgs)
    {
        base.OnDataContextChanged(eventArgs);

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.AboutRequested += OnAboutRequested;
        viewModel.ExitRequested  += (_, _) => Close();
    }

    /// <summary>Shows the About window as a dialogue over this window, and reports any exception it raises.</summary>
    /// <param name="sender">The view model that raised the event.</param>
    /// <param name="eventArgs">The event data.</param>
    private async void OnAboutRequested(object? sender, EventArgs eventArgs)
    {
        try
        {
            await new AboutWindow().ShowDialog(this);
        }
        catch (Exception exception)
        {
            UnexpectedErrorHandler.ReportRecoverable(exception);
        }
    }

    /// <summary>Asks the view model whether the window may close and, when it may, closes it.</summary>
    /// <param name="viewModel">The view model of the window.</param>
    /// <returns>A task that completes when the question has been answered.</returns>
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
        catch (Exception exception)
        {
            UnexpectedErrorHandler.ReportRecoverable(exception);
        }
        finally
        {
            _isAsking = false;
        }
    }
}
