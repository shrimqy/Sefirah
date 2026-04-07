using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IBatteryAlertService
{
    Task HandleBatteryStateAsync(PairedDevice device, BatteryState batteryState);
}
