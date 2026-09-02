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

    // Left = inline expanded (open). Used at ≥1200 and at ≤360 so the pane fills the min window.
    // LeftCompact = rail when closed, overlay when open.
    private const double ExpandedPaneMinWidth = 1200;
    private bool IsInline => SideBar.PaneDisplayMode is NavigationViewPaneDisplayMode.Left;

    private readonly Dictionary<string, Type> Pages = new()
    {
        { "Settings", typeof(SettingsPage) },
        { "Calls", typeof(CallsPage) },
        { "Messages", typeof(MessagesPage) },
        { "Apps", typeof(AppsPage) }
    };

    public MainPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
        DevicesViewModel = Ioc.Default.GetRequiredService<DevicesViewModel>();
        Loaded += MainPage_Loaded;
    }

    private void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyPaneMode(ActualWidth);
        UpdatePaneCustomHeight();
    }

    // Auto cannot expand at both 360 and 1200, so PaneDisplayMode is set from width.
    private NavigationViewPaneDisplayMode NavigationMode(double width)
    {
        if (width <= SideBar.OpenPaneLength || width >= ExpandedPaneMinWidth)
            return NavigationViewPaneDisplayMode.Left;
        return NavigationViewPaneDisplayMode.LeftCompact;
    }

    private void SideBar_SizeChanged(object sender, SizeChangedEventArgs e)
        => ApplyPaneMode(ActualWidth);

    private void ApplyPaneMode(double width)
    {
        // First layout can report the compact rail width, not the window.
        if (width <= SideBar.CompactPaneLength)
            return;

        var mode = NavigationMode(width);
        if (SideBar.PaneDisplayMode == mode)
            return;

        SideBar.PaneDisplayMode = mode;
        var open = mode is NavigationViewPaneDisplayMode.Left;
        if (SideBar.IsPaneOpen != open)
            SideBar.IsPaneOpen = open;

        UpdatePaneVisuals(open);
    }

    // Swap rail/overlay at animation start; IsPaneOpen is still true during Closing.
    private void SideBar_PaneOpening(NavigationView sender, object args)
        => UpdatePaneVisuals(isPaneOpen: true);

    private void SideBar_PaneClosing(NavigationView sender, NavigationViewPaneClosingEventArgs args)
        => UpdatePaneVisuals(isPaneOpen: false);

    private void SideBar_PaneClosed(NavigationView sender, object args)
    {
        if (IsInline)
        {
            // Inline expanded should stay open; a close here would leave a 0-width pane.
            if (!SideBar.IsPaneOpen)
                SideBar.IsPaneOpen = true;
            return;
        }

        // Compact overlay sometimes skips PaneClosing; last visuals would stay overlay.
        UpdatePaneVisuals(isPaneOpen: false);
    }

    private void SideBar_LayoutUpdated(object? sender, object e)
        => UpdatePaneCustomHeight();

    private void OpenSideBar_Click(object sender, RoutedEventArgs e) => SideBar.IsPaneOpen = true;

    private void CloseSideBar_Click(object sender, RoutedEventArgs e) => SideBar.IsPaneOpen = false;

    private void CompactPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        var session = button.DataContext as MediaSession ?? button.Tag as MediaSession;
        if (session is null)
            return;

        var action = session.IsPlaying ? MediaActionType.Pause : MediaActionType.Play;
        ViewModel.HandlePlaybackAction(session, action);
    }

    // PaneCustomContent does not stretch to the nav height on its own.
    private void UpdatePaneCustomHeight()
    {
        var height = SideBar.ActualHeight;
        if (height > 0 && PaneCustomRoot.Height != height)
            PaneCustomRoot.Height = height;
    }

    private void UpdatePaneVisuals(bool? isPaneOpen = null)
    {
        var paneOpen = isPaneOpen ?? SideBar.IsPaneOpen;
        // Closed compact = rail; open overlay or Left = full pane. No close chevron when inline.
        var rail = !paneOpen && !IsInline;

        CompactPaneContent.Visibility = rail ? Visibility.Visible : Visibility.Collapsed;
        ExpandedPaneContent.Visibility = rail ? Visibility.Collapsed : Visibility.Visible;
        CollapsePaneButton.Visibility = !IsInline && paneOpen ? Visibility.Visible : Visibility.Collapsed;
    }

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
