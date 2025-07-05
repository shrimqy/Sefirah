using Uno.Logging;

namespace Sefirah.Utils;

public static class ProcessExecutor
{
    public static void ExecuteProcess(string fileName, string? arguments)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            Arguments = arguments ?? string.Empty,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }

    public static void ExecuteDelayed(string fileName, string arguments, int delay)
    {
        Task.Run(async () =>
        {
            await Task.Delay(delay * 1000);
            ExecuteProcess(fileName, arguments);
        });
    }
} 
