using Sefirah.Data.Contracts;
using Sefirah.Services;
using Uno.Logging;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopActionService(
    IGeneralSettingsService generalSettingsService, 
    ISessionManager sessionManager, 
    ILogger<DesktopActionService> logger) : BaseActionService(generalSettingsService, sessionManager, logger)
{
}
