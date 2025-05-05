using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Utils;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

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

    public void OnMenuFlyoutItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem menuItem && 
            menuItem.Tag is ApplicationInfoEntity settings)
        {
            ViewModel.ChangeNotificationFilter(settings);
        }
    }

    public async void SelectScrcpyLocation_Click(object sender, RoutedEventArgs e)
    {
        var path = await LocationPicker.FileLocationPicker();

        if (!string.IsNullOrEmpty(path))
        {
            ViewModel.ScrcpyPath = path;

            var directory = Path.GetDirectoryName(path);
            if(string.IsNullOrEmpty(directory) || !string.IsNullOrEmpty(ViewModel.AdbPath)) return;
            var adbPath = Path.GetFullPath(Path.Combine(directory, "adb.exe"));
            if (File.Exists(adbPath))
            {
                ViewModel.AdbPath = adbPath;
            }
        }
    }

    private async void SelectAdbLocation_Click(object sender, RoutedEventArgs e)
    {
        var path = await LocationPicker.FileLocationPicker();

        if (!string.IsNullOrEmpty(path))
        {
            ViewModel.AdbPath = path;

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory) || !string.IsNullOrEmpty(ViewModel.ScrcpyPath)) return;
            var scrcpyPath = Path.GetFullPath(Path.Combine(directory, "scrcpy.exe"));
            if (File.Exists(scrcpyPath))
            {
                ViewModel.ScrcpyPath = scrcpyPath;
            }
        }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            this.Focus(FocusState.Pointer);
            e.Handled = true;
        }
    }

    private void DisplayTextSubmitted(object sender, ComboBoxTextSubmittedEventArgs e)
    {
        ViewModel.Display = e.Text;
        e.Handled = true;
    }
}
