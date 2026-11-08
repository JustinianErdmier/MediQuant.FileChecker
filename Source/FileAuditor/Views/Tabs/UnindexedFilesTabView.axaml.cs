namespace FileAuditor.Views.Tabs;

/// <summary>The Unindexed Files tab page.</summary>
public partial class UnindexedFilesTabView : UserControl
{
    /// <summary>Initialises a new instance of the <see cref="UnindexedFilesTabView" /> class.</summary>
    public UnindexedFilesTabView() => InitializeComponent();

    private async void OnAddClick(object? sender, RoutedEventArgs eventArgs)
    {
        try
        {
            if (DataContext is not UnindexedFilesTabViewModel viewModel
                || TopLevel.GetTopLevel(this) is not { } topLevel)
            {
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title         = "Select a folder to check",
                AllowMultiple = false
            });

            if (folders.Count > 0
                && folders[index: 0]
                    .TryGetLocalPath() is { } path)
            {
                viewModel.AddFolder(path);
            }
        }
        catch (Exception exception)
        {
            UnexpectedErrorHandler.ReportRecoverable(exception);
        }
    }
}
