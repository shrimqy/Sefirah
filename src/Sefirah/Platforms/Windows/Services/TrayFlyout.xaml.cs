using DesktopFlyouts;
using Sefirah.ViewModels;
using WinSystemThemeHelper = Sefirah.Platforms.Windows.Helpers.SystemThemeHelper;

namespace Sefirah.Platforms.Windows.Services;

public sealed partial class TrayFlyout : DesktopFlyout
{
    public MainPageViewModel ViewModel { get; }

    public TrayFlyout()
    {
        ViewModel = Ioc.Default.GetRequiredService<MainPageViewModel>();
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
        UpdatePopupDirection();
        base.Show();
    }

    private void UpdatePopupDirection()
    {
        PopupDirection = NotificationIsland.Visibility is Visibility.Visible
            ? DesktopFlyoutPopupDirection.RightToLeft
            : DesktopFlyoutPopupDirection.BottomToTop;
    }
}
