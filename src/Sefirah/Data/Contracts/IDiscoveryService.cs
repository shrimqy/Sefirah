using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;
public interface IDiscoveryService
{
    /// <summary>
    /// The list of discovered devices.
    /// </summary>
    ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Starts the udp discovery process.
    /// </summary>
    Task StartDiscoveryAsync(int serverPort);

    void Dispose();
}
