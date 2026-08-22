using DesktopFlyouts;
using Sefirah.Data.Models;
using Sefirah.Views;
using Sefirah.ViewModels;
using WinSystemThemeHelper = Sefirah.Platforms.Windows.Helpers.SystemThemeHelper;

namespace Sefirah.Platforms.Windows.Services;

public sealed partial class TrayFlyout : DesktopFlyout
{
    public MainPageViewModel ViewModel { get; }
    public AppsViewModel AppsViewModel { get; }

    public TrayFlyout()
    {
        ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
        AppsViewModel = Ioc.Default.GetRequiredService<AppsViewModel>();
        InitializeComponent();

        ApplySystemTheme();
    }

    // to be removed https://github.com/0x5bfa/DesktopFlyouts/pull/20 is merged
    public void ApplySystemTheme()
    {
        DispatcherQueue.TryEnqueue(() => 
        {
            var theme = WinSystemThemeHelper.SystemUsesLightTheme() ? ElementTheme.Light : ElementTheme.Dark; ;
            RequestedTheme = theme;
        });
    }

    public new void Show()
    {
        // don't show flyout if there are no devices for now atleast
        if (ViewModel.PairedDevices.Count == 0)
            return;

        UpdatePopupDirection();
        base.Show();
    }

    private void UpdatePopupDirection()
    {
        PopupDirection = NotificationIsland.Visibility is Visibility.Visible
            ? DesktopFlyoutPopupDirection.RightToLeft
            : DesktopFlyoutPopupDirection.BottomToTop;
    }

    private async void PinnedApp_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not ApplicationItem app)
            return;

        Hide();
        await AppsViewModel.OpenApp(app);
    }

    private void Navigate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string pageTag })
            return;

        Hide();
        ApplicationData.Current.LocalSettings.Values[Constants.LocalSettings.MainNavigationSelection] = pageTag;

        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            if (App.MainWindow.Content is Frame { Content: MainPage mainPage })
                mainPage.NavigateToPage(pageTag);

            App.ShowMainWindow();
        });
    }
}
