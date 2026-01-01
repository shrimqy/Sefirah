using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IDiscoveryService
{
    /// <summary>
    /// Starts the udp discovery process.
    /// </summary>
    Task StartDiscoveryAsync();

    void StopDiscovery();

    /// <summary>
    /// Gets the current UDP broadcast data.
    /// </summary>
    UdpBroadcast? BroadcastMessage { get; }
}
