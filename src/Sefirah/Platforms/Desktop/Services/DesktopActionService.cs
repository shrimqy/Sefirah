using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopActionService(ILogger logger) : IActionService
{
    public void HandleDefaultCommand(ActionMessage action)
    {
        switch (action.ActionType)
        {
            case ActionType.Shutdown:
                ProcessExecutor.ExecuteProcess("shutdown", $"-h +{ConvertSecondsToMinutes(action.Value)}");
                break;
            case ActionType.Restart:
                ProcessExecutor.ExecuteProcess("shutdown", $"-r +{ConvertSecondsToMinutes(action.Value)}");
                break;
            case ActionType.Hibernate:
                ProcessExecutor.ExecuteDelayed("systemctl", "hibernate", Convert.ToInt32(action.Value));
                break;
            case ActionType.Lock:
                ProcessExecutor.ExecuteDelayed("loginctl", "lock-session", Convert.ToInt32(action.Value));
                break;
            case ActionType.Logoff:
                ProcessExecutor.ExecuteDelayed("loginctl", "terminate-session", Convert.ToInt32(action.Value));
                break;
            case ActionType.Sleep:
                ProcessExecutor.ExecuteDelayed("systemctl", "suspend", Convert.ToInt32(action.Value));
                break;
            default:
                logger.LogWarning("Action type not configured: {ActionType}", action.ActionType);
                break;
        }
    }

    public void HandleCustomAction(CustomActionMessage action)
    {
        logger.LogInformation("Executing custom action: {Path} {Arguments}", 
            action.Path, action.Arguments);
        
        ProcessExecutor.ExecuteProcess(action.Path, action.Arguments);
    }

    private static int ConvertSecondsToMinutes(string? seconds)
    {
        // Linux shutdown uses minutes, convert from seconds
        return Math.Max(1, Convert.ToInt32(seconds) / 60);
    }
}
