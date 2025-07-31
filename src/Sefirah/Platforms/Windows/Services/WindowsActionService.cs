using Sefirah.Data.Contracts;
using Sefirah.Services;
using Uno.Logging;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsActionService(
    IGeneralSettingsService generalSettingsService,
    ISessionManager sessionManager,
    ILogger<WindowsActionService> logger) : BaseActionService(generalSettingsService, sessionManager, logger)
{
}
