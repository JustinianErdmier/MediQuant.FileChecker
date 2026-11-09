namespace FileAuditor.Views.Groups;

/// <summary>The Report group box.</summary>
public partial class ReportView : UserControl
{
    /// <summary>Initialises a new instance of the <see cref="ReportView" /> class.</summary>
    public ReportView() => InitializeComponent();

    /// <summary>Opens the folder picker when Browse is clicked and, when a folder is chosen, makes it the report location.</summary>
    /// <param name="sender">The Browse button.</param>
    /// <param name="eventArgs">The event data.</param>
    private async void OnBrowseClick(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            if (DataContext is not ReportViewModel viewModel
                || TopLevel.GetTopLevel(this) is not { } topLevel)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title         = "Select the report folder",
                AllowMultiple = false
            });

            if (folders.Count > 0
                && folders[index: 0]
                    .TryGetLocalPath() is { } path)
            {
                viewModel.Location = path;
            }
        }
        catch (Exception exception)
        {
            UnexpectedErrorHandler.ReportRecoverable(exception);
        }
    }
}
