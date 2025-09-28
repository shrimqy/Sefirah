using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Data.Items;
using Sefirah.Extensions;
using Sefirah.Utils;
using Windows.System;

namespace Sefirah.Views.Settings;

public sealed partial class GeneralPage : Page
{
    public GeneralPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            Focus(FocusState.Pointer);
            e.Handled = true;
        }
    }

    private void SetupBreadcrumb()
    {
        BreadcrumbBar.ItemsSource = new ObservableCollection<BreadcrumbBarItemModel>
        {
            new("General".GetLocalizedResource(), typeof(GeneralPage))
        };
        BreadcrumbBar.ItemClicked += BreadcrumbBar_ItemClicked;
    }
    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbBarItemModel>;
        var clickedItem = items?[args.Index];

        if (clickedItem?.PageType != typeof(ActionsPage))
        {
            // Navigate back to general page
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }

    public async void SelectSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        if (await PickerHelper.PickFolderAsync() is StorageFolder folder)
        {
            ViewModel.ReceivedFilesPath = folder.Path;
        }
    }

    public async void SelectRemoteLocation_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Warning: Remote Storage Location",
            Content = "DO NOT set the remote storage location to a pre-existing folder as it will delete the contents of that folder. Are you sure you want to continue?",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (await PickerHelper.PickFolderAsync() is StorageFolder folder)
            {
                ViewModel.RemoteStoragePath = folder.Path;
            }
        }
    }

    public async void SelectScrcpyLocation_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickFileAsync();
        if (file?.Path is string path)
        {
            ViewModel.ScrcpyPath = path;
            TrySetCompanionTool(path, "adb.exe", p => ViewModel.AdbPath = p);
        }
    }

    private async void SelectAdbLocation_Click(object sender, RoutedEventArgs e)
    {
        var file = await PickerHelper.PickFileAsync();
        if (file?.Path is string path)
        {
            ViewModel.AdbPath = path;
            TrySetCompanionTool(path, "scrcpy.exe", p => ViewModel.ScrcpyPath = p);
        }
    }

    public static void TrySetCompanionTool(string selectedPath, string companionName, Action<string> setPath)
    {
        var directory = Path.GetDirectoryName(selectedPath);
        if (string.IsNullOrEmpty(directory)) return;

        var companionPath = Path.GetFullPath(Path.Combine(directory, companionName));
        if (File.Exists(companionPath))
        {
            setPath(companionPath);
        }
    }

    private void OpenActionsSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ActionsPage), null, new SuppressNavigationTransitionInfo());
    }
}
 
