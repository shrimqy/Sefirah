using Sefirah.Data.Models.Actions;

namespace Sefirah.Platforms.Windows;

public class WindowsDefaultActions
{
    public static IReadOnlyList<BaseAction> GetDefaultActions()
    {
        return
        [
            new ProcessAction { Id = "lock", Name = "Lock Screen", Path = "rundll32.exe", Arguments = "user32.dll,LockWorkStation" },
            new ProcessAction { Id = "hibernate", Name = "Hibernate", Path = "shutdown", Arguments = "/h" },
            new ProcessAction { Id = "logoff", Name = "Log Off", Path = "shutdown", Arguments = "/l" },
            new ProcessAction { Id = "restart", Name = "Restart", Path = "shutdown", Arguments = "/r /t 0" },
            new ProcessAction { Id = "shutdown", Name = "Shutdown", Path = "shutdown", Arguments = "/s /t 0" },
        ];
    }
} 
