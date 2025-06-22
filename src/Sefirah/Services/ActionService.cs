using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Uno.Logging;

namespace Sefirah.Services;

public class ActionService(ILogger logger)
{
    public void ExecuteProcess(string fileName, string arguments)
    {
        logger.LogInformation("Executing process: {FileName} {Arguments}", fileName, arguments);
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    public void ExecuteDelayed(string fileName, string arguments, int delay)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delay * 1000);
            ExecuteProcess(fileName, arguments);
        });
    }
}
