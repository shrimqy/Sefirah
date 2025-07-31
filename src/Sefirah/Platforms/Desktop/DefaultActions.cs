using Sefirah.Data.Models.Actions;

namespace Sefirah.Platforms.Desktop;

public class DesktopDefaultActions
{
    public static IReadOnlyList<BaseAction> GetDefaultActions()
    {
        return
        [
            new ProcessAction { Id = "lock", Name = "Lock Screen", Path = "loginctl", Arguments = "lock-session" },
            new ProcessAction { Id = "hibernate", Name = "Hibernate", Path = "systemctl", Arguments = "hibernate" },
            new ProcessAction { Id = "logoff", Name = "Log Off", Path = "loginctl", Arguments = "terminate-session" },
            new ProcessAction { Id = "restart", Name = "Restart", Path = "shutdown", Arguments = "-r now" },
            new ProcessAction { Id = "shutdown", Name = "Shutdown", Path = "shutdown", Arguments = "-h now" },
        ];
    }
} 
