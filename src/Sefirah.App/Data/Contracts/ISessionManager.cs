using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.EventArguments;

namespace Sefirah.App.Data.Contracts;
public interface ISessionManager
{
    /// <summary>
    /// Event raised when client connection status changes, providing session connection details via ConnectedSessionArgs.
    /// </summary>
    event EventHandler<ConnectedSessionEventArgs> ClientConnectionStatusChanged;

    /// <summary>
    /// Returns true if the session is connected, false otherwise.
    /// </summary>
    /// <returns>True if the session is connected, false otherwise.</returns>
    bool IsConnected(); 

    /// <summary>
    /// Sends a message to the connected client.
    /// </summary>
    /// <param name="message">The message to send.</param>
    void SendMessage(string message);

    /// <summary>
    /// Disconnects the current session.
    /// </summary>
    /// <param name="removeSession">Whether to remove the session from the manager.</param>
    void DisconnectSession(bool removeSession = false);

    /// <summary>
    /// Gets the currently connected device.
    /// </summary>
    /// <returns>The currently connected device.</returns>
    RemoteDeviceEntity? GetCurrentlyConnectedDevice();
}
