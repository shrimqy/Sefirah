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

    /// <summary>Disconnects the active session or client for a paired device.</summary>
    void DisconnectDevice(PairedDevice device, bool forcedDisconnect = false);

    /// <summary>Connects to a paired device using its enabled addresses. Cancels an in-progress attempt if one exists.</summary>
    void Connect(PairedDevice device);

    /// <summary>Connects to a paired device at a specific address. Cancels any in-progress attempt first.</summary>
    void Connect(PairedDevice device, string address);

    void Pair(DiscoveredDevice device);

    /// <summary>
    /// Discovery-driven connect. Skips the attempt when the device
    /// is already paired and connected, connecting, or force-disconnected. For unknown
    /// devices, opens a pre-pairing client connection.
    /// </summary>
    void Connect(string deviceId, string address, int port);
}
