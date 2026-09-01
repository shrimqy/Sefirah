using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class FilesPage : Page
{
    public FilesViewModel ViewModel { get; }

    public FilesPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<FilesViewModel>();

        PathBreadcrumbBar.ItemClicked += PathBreadcrumbBar_ItemClicked;
        Loaded += FilesPage_Loaded;
        Unloaded += FilesPage_Unloaded;
    }

    private async void FilesPage_Loaded(object sender, RoutedEventArgs e)
        => await ViewModel.InitializeAsync();

    private void FilesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        PathBreadcrumbBar.ItemClicked -= PathBreadcrumbBar_ItemClicked;
        Loaded -= FilesPage_Loaded;
        Unloaded -= FilesPage_Unloaded;
        ViewModel.Suspend();
    }

    private async void PathBreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
        => await ViewModel.NavigateToSegmentAsync(args.Index);

    private async void FilesListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RemoteFileItem item)
        {
            await ViewModel.OpenCommand.ExecuteAsync(item);
        }
    }

    private async void OpenItemClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is RemoteFileItem item)
        {
            await ViewModel.OpenCommand.ExecuteAsync(item);
        }
    }

    private async void DownloadItemClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is RemoteFileItem item)
        {
            await ViewModel.DownloadCommand.ExecuteAsync(item);
        }
    }

    private async void RenameItemClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is RemoteFileItem item)
        {
            await ViewModel.RenameCommand.ExecuteAsync(item);
        }
    }

    private async void DeleteItemClick(object sender, RoutedEventArgs e)
    {
        if (GetItem(sender) is RemoteFileItem item)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(item);
        }
    }

    private static RemoteFileItem? GetItem(object sender)
        => (sender as FrameworkElement)?.DataContext as RemoteFileItem;
}
