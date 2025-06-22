using Sefirah.Data.Models;
using Sefirah.Services.Socket;

namespace Sefirah.Data.Contracts;
public interface ISessionManager
{
    /// <summary>
    /// Event fired when a device connection status changes
    /// </summary>
    event EventHandler<(PairedDevice Device, bool IsConnected)> ConnectionStatusChanged;

    /// <summary>
    /// Sends a message to the connected client.
    /// </summary>
    /// <param name="message">The message to send.</param>
    void SendMessage(ServerSession session, string message);

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message to send.</param>
    void BroadcastMessage(string message);

    /// <summary>
    /// Disconnects the current session.
    /// </summary>
    void DisconnectSession(ServerSession session);
}
