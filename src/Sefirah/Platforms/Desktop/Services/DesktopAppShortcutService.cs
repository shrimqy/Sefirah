using Sefirah.Data.Contracts;
using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopAppShortcutService : IAppShortcutService
{
    public Task CreateAppShortcutAsync(ApplicationItem app)
    {
        return Task.CompletedTask;
    }

    public Task RemoveAppShortcutAsync(string androidPackageName)
    {
        return Task.CompletedTask;
    }
}
