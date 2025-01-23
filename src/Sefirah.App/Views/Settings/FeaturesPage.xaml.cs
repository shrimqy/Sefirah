using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.AppDatabase.Models;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace Sefirah.App.Views.Settings;

public sealed partial class FeaturesPage : Page
{
    public FeaturesPage()
    {
        InitializeComponent();
    }

    public async void SelectSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        var window = MainWindow.Instance;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, 
            WinRT.Interop.WindowNative.GetWindowHandle(window));

        if (await picker.PickSingleFolderAsync() is StorageFolder folder)
        {
            ViewModel.ReceivedFilesPath = folder.Path;
        }
    }

    public async void SelectRemoteLocation_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        var window = MainWindow.Instance;
        WinRT.Interop.InitializeWithWindow.Initialize(picker, 
            WinRT.Interop.WindowNative.GetWindowHandle(window));

        if (await picker.PickSingleFolderAsync() is StorageFolder folder)
        {
            ViewModel.RemoteStoragePath = folder.Path;
        }
    }

    public void OnMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && 
            menuItem.Tag is ApplicationInfoEntity settings)
        {
            ViewModel.ChangeNotificationFilter(settings);
        }
    }
}
