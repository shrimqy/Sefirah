using Sefirah.Data.Contracts;
using Sefirah.Services;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsActionService(
    IGeneralSettingsService generalSettingsService,
    ISessionManager sessionManager,
    IUserSettingsService userSettingsService,
    ILogger<WindowsActionService> logger) : BaseActionService(generalSettingsService, userSettingsService, sessionManager, logger)
{
}
