using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IBatteryFeature : IFeature
{
    void SendBatteryStatus(PairedDevice device);
}
