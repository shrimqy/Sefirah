using Sefirah.Data.Models.Actions;
#if WINDOWS
using Sefirah.Platforms.Windows;
#elif DESKTOP
using Sefirah.Platforms.Desktop;
#endif

namespace Sefirah.Services;

public static class DefaultActionsProvider
{
    public static IEnumerable<BaseAction> GetDefaultActions()
    {
#if WINDOWS
        return WindowsDefaultActions.GetDefaultActions();
#elif DESKTOP
        return DesktopDefaultActions.GetDefaultActions();
#endif
    }
} 
