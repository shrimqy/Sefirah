namespace Sefirah.App.Services;
using System.Diagnostics;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;

public class CommandService(ILogger logger) : ICommandService
{
    public void HandleCommand(CommandMessage command)
    {
        switch (command.CommandType)
        {
            case CommandType.Shutdown:
                ExecuteProcess("shutdown", $"/s /t {command.Value}");
                break;
            case CommandType.Restart:
                ExecuteProcess("shutdown", $"/r /t {command.Value}");
                break;
            case CommandType.Hibernate:
                ExecuteDelayed("shutdown", "/h", Convert.ToInt32(command.Value));
                break;
            case CommandType.Lock:
                ExecuteDelayed("rundll32.exe", "user32.dll,LockWorkStation", Convert.ToInt32(command.Value));
                break;
            case CommandType.Logoff:
                ExecuteDelayed("shutdown", "/l", Convert.ToInt32(command.Value));
                break;
            case CommandType.Sleep:
                ExecuteDelayed("rundll32.exe", "powrprof.dll,SetSuspendState", Convert.ToInt32(command.Value));
                break;
            default:
                logger.Warn("Unknown command type: {CommandType}", command.CommandType);
                break;
        }
    }

    private void ExecuteProcess(string fileName, string arguments)
    {
        logger.Info("Executing process: {FileName} {Arguments}", fileName, arguments);
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    private void ExecuteDelayed(string fileName, string arguments, int delay)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delay * 1000);
            ExecuteProcess(fileName, arguments);
        });
    }
}