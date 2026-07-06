using Microsoft.UI.Xaml.Input;
using Sefirah.Data.Models;
using Sefirah.ViewModels;
using Sefirah.ViewModels.Settings;
using Windows.ApplicationModel.DataTransfer;

namespace Sefirah.Views;

public sealed partial class MainPage : Page
{
    public MainPageViewModel ViewModel { get; }
    public DevicesViewModel DevicesViewModel { get; }
    private readonly ISessionManager SessionManager = Ioc.Default.GetRequiredService<ISessionManager>();

    public MainPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
        DevicesViewModel = Ioc.Default.GetRequiredService<DevicesViewModel>();
        Unloaded += MainPage_Unloaded;
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
        => MainNavigationView.Loaded -= MainNavigationView_Loaded;

    private void MainNavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Device is null)
        {
            MainNavigationView.SelectedItem = MainNavigationView.SettingsItem;
            return;
        }

        if (ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.MainNavigationSelection] is not string lastActivePage ||
            !Pages.ContainsKey(lastActivePage))
        {
            MainNavigationView.SelectedItem = MainNavigationView.SettingsItem;
            return;
        }

        MainNavigationView.SelectedItem = lastActivePage switch
        {
            "Settings" => MainNavigationView.SettingsItem,
            "Calls" => CallsNavigationItem,
            "Messages" => MessagesNavigationItem,
            "Apps" => AppsNavigationItem,
            _ => MainNavigationView.SettingsItem,
        };
    }

    private readonly Dictionary<string, Type> Pages = new()
    {
        { "Settings", typeof(SettingsPage) },
        { "Calls", typeof(CallsPage) },
        { "Messages", typeof(MessagesPage) },
        { "Apps", typeof(AppsPage) }
    };

    private void NavigationView_SelectionChanged(NavigationView _, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem &&
            selectedItem.Tag?.ToString() is string tag &&
            Pages.TryGetValue(tag, out Type? pageType))
        {
            ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.MainNavigationSelection] = tag;
            ContentFrame.Navigate(pageType);
        }
    }

    private void DiscoveredDeviceButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DiscoveredDevice device)
            SessionManager.Pair(device);
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        // Check if the dropped data contains files
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
            ViewModel.SendFiles(await e.DataView.GetStorageItemsAsync());
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        if (ViewModel.PairedDevices.Count == 0) return;

        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "FileDropCaption".GetLocalizedResource();
    }
}
