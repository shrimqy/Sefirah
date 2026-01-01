using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface ISessionManager
{
    /// <summary>
    /// Event fired when a device connection status changes
    /// </summary>
    event EventHandler<PairedDevice> ConnectionStatusChanged;

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message to send.</param>
    void BroadcastMessage(SocketMessage message);

    void DisconnectDevice(PairedDevice device, bool forcedDisconnect = false);

    Task ConnectTo(PairedDevice device);

    Task Pair(DiscoveredDevice device);

    Task ConnectTo(string deviceId, string host, int port, string publicKey);
}
