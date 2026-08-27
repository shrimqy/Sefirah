using Sefirah.Data.Models;
using Sefirah.ViewModels;

namespace Sefirah.Views;

public sealed partial class PhotosPage : Page
{
    public PhotosViewModel ViewModel { get; }

    public PhotosPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<PhotosViewModel>();

        Loaded += PhotosPage_Loaded;
        Unloaded += PhotosPage_Unloaded;
    }

    private async void PhotosPage_Loaded(object sender, RoutedEventArgs e)
        => await ViewModel.InitializeAsync();

    private void PhotosPage_Unloaded(object sender, RoutedEventArgs e)
    {
        Loaded -= PhotosPage_Loaded;
        Unloaded -= PhotosPage_Unloaded;
        ViewModel.Suspend();
    }

    private async void PhotosGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RemotePhotoItem photo)
        {
            await ViewModel.OpenCommand.ExecuteAsync(photo);
        }
    }

    private async void OpenPhotoClick(object sender, RoutedEventArgs e)
    {
        if (GetPhoto(sender) is RemotePhotoItem photo)
        {
            await ViewModel.OpenCommand.ExecuteAsync(photo);
        }
    }

    private async void CopyPhotoClick(object sender, RoutedEventArgs e)
    {
        if (GetPhoto(sender) is RemotePhotoItem photo)
        {
            await ViewModel.CopyCommand.ExecuteAsync(photo);
        }
    }

    private async void SavePhotoClick(object sender, RoutedEventArgs e)
    {
        if (GetPhoto(sender) is RemotePhotoItem photo)
        {
            await ViewModel.SaveCommand.ExecuteAsync(photo);
        }
    }

    private async void DeletePhotoClick(object sender, RoutedEventArgs e)
    {
        if (GetPhoto(sender) is RemotePhotoItem photo)
        {
            await ViewModel.DeleteCommand.ExecuteAsync(photo);
        }
    }

    private static RemotePhotoItem? GetPhoto(object sender)
        => (sender as FrameworkElement)?.DataContext as RemotePhotoItem;
}
