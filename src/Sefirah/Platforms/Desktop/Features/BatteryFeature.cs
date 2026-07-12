using Sefirah.Data.Models;

namespace Sefirah.Platforms.Desktop.Features;

public class BatteryFeature : IBatteryFeature
{
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public void SendBatteryStatus(PairedDevice device)
    {
    }
}
