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
        
        // Show warning dialog before setting the path
        var dialog = new ContentDialog 
        {
            Title = "Warning: Remote Storage Location",
            Content = "DO NOT set the remote storage location to a pre-existing folder as it will delete the contents of that folder. Are you sure you want to continue?",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
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
                // User confirmed, update the path
                ViewModel.RemoteStoragePath = folder.Path;
            }
        }

    }

    public async void SelectScrcpyLocation_Click(object sender, RoutedEventArgs e)
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
            ViewModel.ScrcpyPath = folder.Path;
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
