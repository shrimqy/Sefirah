using System.Security.Cryptography.X509Certificates;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;
public interface IDiscoveryService
{
    ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; }
    Task StartDiscoveryAsync(string serverIpAddress, int serverPort, X509Certificate2 certificate);
}
