using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IBatteryService
{
    Task InitializeAsync();

    void SendBatteryStatus(PairedDevice device);
}
