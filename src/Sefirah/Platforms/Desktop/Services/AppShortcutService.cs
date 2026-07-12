using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Services;

public class AppShortcutService : IAppShortcutService
{
    public Task CreateAppShortcutAsync(ApplicationItem app)
    {
        return Task.CompletedTask;
    }

    public Task RemoveAppShortcutAsync(string androidPackageName)
    {
        return Task.CompletedTask;
    }

    public bool IsShortcutRegistered(string androidPackageName) => false;
}
