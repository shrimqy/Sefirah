using DesktopFlyouts;

namespace Sefirah.Platforms.Windows.Services;

public sealed partial class TrayContextMenu : DesktopMenuFlyout
{
    public TrayContextMenu(Action onShowHide, Action onExit)
    {
        InitializeComponent();
        ShowHideItem.Click += (_, _) => onShowHide();
        ExitItem.Click += (_, _) => onExit();
    }
}
