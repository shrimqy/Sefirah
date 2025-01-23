using MeaMod.DNS.Multicast;
using Sefirah.App.Data.EventArguments;

namespace Sefirah.App.Data.Contracts;

/// <summary>
/// mDNS is primarily used in the pairing process to exchange information, and to advertise the device on the local network so that android app can discover.
/// </summary>
public interface IMdnsService
{
    /// <summary>
    /// Event triggered when a new device is discovered via local network.
    /// </summary>
    event EventHandler<DiscoveredMdnsServiceArgs>? DiscoveredMdnsService;

    /// <summary>
    /// Event triggered when a previously discovered device is lost.
    /// </summary>
    event EventHandler<ServiceInstanceShutdownEventArgs>? ServiceInstanceShutdown;

    /// <summary>
    /// Advertises the device to the local network.
    /// </summary>
    void AdvertiseService(int port);

    /// <summary>
    /// Stops advertising the mDNS service.
    /// </summary>
    void UnAdvertiseService();

    /// <summary>
    /// Starts the mDNS discovery.
    /// </summary>
    void StartDiscovery();
}