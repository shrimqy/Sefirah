using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IBatteryAlertFeature : IFeature
{
    Task HandleBatteryStateAsync(PairedDevice device, BatteryState batteryState);
}
