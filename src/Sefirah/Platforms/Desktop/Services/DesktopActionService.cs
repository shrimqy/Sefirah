using Sefirah.Data.Contracts;
using Sefirah.Services;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopActionService(
    IGeneralSettingsService generalSettingsService, 
    IUserSettingsService userSettingsService,
    ISessionManager sessionManager, 
    ILogger<DesktopActionService> logger) : BaseActionService(generalSettingsService, userSettingsService, sessionManager, logger)
{
}
