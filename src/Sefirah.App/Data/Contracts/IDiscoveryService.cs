using System.Security.Cryptography.X509Certificates;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;
public interface IDiscoveryService
{
    /// <summary>
    /// The list of discovered devices.
    /// </summary>
    ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Starts the udp discovery process.
    /// </summary>
    Task StartDiscoveryAsync(string serverIpAddress, int serverPort, X509Certificate2 certificate);
}
