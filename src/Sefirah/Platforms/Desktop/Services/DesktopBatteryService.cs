using Sefirah.Data.Contracts;
using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopBatteryService : IBatteryService
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public void SendBatteryStatus(PairedDevice device)
    {
    }
}
