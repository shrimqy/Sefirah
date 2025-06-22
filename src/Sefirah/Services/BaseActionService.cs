using Uno.Logging;

namespace Sefirah.Services;

public abstract class BaseActionService(ILogger logger)
{
    protected readonly ILogger Logger = logger;

    protected void ExecuteProcess(string fileName, string arguments)
    {
        Logger.LogInformation("Executing process: {FileName} {Arguments}", fileName, arguments);
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    protected void ExecuteDelayed(string fileName, string arguments, int delay)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delay * 1000);
            ExecuteProcess(fileName, arguments);
        });
    }
} 