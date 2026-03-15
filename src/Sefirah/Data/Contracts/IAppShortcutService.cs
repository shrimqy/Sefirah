using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

/// <summary>
/// Registers and unregisters hosted app packages (e.g. for pinned Android apps) so they appear as separate apps in Start
/// </summary>
public interface IAppShortcutService
{
    Task CreateAppShortcutAsync(ApplicationItem app);
    
    Task RemoveAppShortcutAsync(string androidPackageName);
}
