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

    private readonly Dictionary<string, Type> Pages = new()
    {
        { "Settings", typeof(SettingsPage) },
        { "Calls", typeof(CallsPage) },
        { "Messages", typeof(MessagesPage) },
        { "Apps", typeof(AppsPage) },
        { "Files", typeof(FilesPage) },
        { "Photos", typeof(PhotosPage) },
        { "Clipboard", typeof(ClipboardPage) }
    };

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
        var savedPage = ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.MainNavigationSelection] as string;
        var page = ViewModel.Device is not null && savedPage is not null && Pages.ContainsKey(savedPage)
            ? savedPage
            : "Settings";

        NavigateToPage(page);
    }

    private void NavigationView_SelectionChanged(NavigationView _, NavigationViewSelectionChangedEventArgs args)
    {
        string? tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString();

        if (tag is null || !Pages.TryGetValue(tag, out Type? pageType))
            return;

        ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.MainNavigationSelection] = tag;
        ContentFrame.Navigate(pageType);
    }

    public void NavigateToPage(string pageTag)
    {
        if (!Pages.ContainsKey(pageTag))
            return;

        MainNavigationView.SelectedItem = pageTag switch
        {
            "Calls" => CallsNavigationItem,
            "Messages" => MessagesNavigationItem,
            "Apps" => AppsNavigationItem,
            "Files" => FilesNavigationItem,
            "Photos" => PhotosNavigationItem,
            "Clipboard" => ClipboardNavigationItem,
            "Settings" => MainNavigationView.SettingsItem,
            _ => MainNavigationView.SelectedItem
        };
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
