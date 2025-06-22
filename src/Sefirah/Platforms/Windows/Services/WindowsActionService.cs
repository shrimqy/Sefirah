using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;
using Uno.Logging;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsActionService(ILogger logger) : IActionService
{
    public void HandleDefaultCommand(ActionMessage action)
    {
        switch (action.ActionType)
        {
            case ActionType.Shutdown:
                ProcessExecutor.ExecuteProcess("shutdown", $"/s /t {action.Value}");
                break;
            case ActionType.Restart:
                ProcessExecutor.ExecuteProcess("shutdown", $"/r /t {action.Value}");
                break;
            case ActionType.Hibernate:
                ProcessExecutor.ExecuteDelayed("shutdown", "/h", Convert.ToInt32(action.Value));
                break;
            case ActionType.Lock:
                ProcessExecutor.ExecuteDelayed("rundll32.exe", "user32.dll,LockWorkStation", Convert.ToInt32(action.Value));
                break;
            case ActionType.Logoff:
                ProcessExecutor.ExecuteDelayed("shutdown", "/l", Convert.ToInt32(action.Value));
                break;
            case ActionType.Sleep:
                ProcessExecutor.ExecuteDelayed("rundll32.exe", "powrprof.dll,SetSuspendState", Convert.ToInt32(action.Value));
                break;
            default:
                logger.LogWarning("Action type not configured: {ActionType}", action.ActionType);
                break;
        }
    }

    public void HandleCustomAction(CustomActionMessage action)
    {
        logger.LogInformation("Executing custom action: {Executable} {Arguments}", 
            action.Path, action.Arguments);
        
        ProcessExecutor.ExecuteProcess(action.Path, action.Arguments);
    }
}
