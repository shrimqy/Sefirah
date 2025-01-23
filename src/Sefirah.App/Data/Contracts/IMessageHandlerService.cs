using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;

public interface IMessageHandlerService
{
    /// <summary>
    /// Handles a JSON message received from a client.
    /// </summary>
    /// <param name="message">The JSON message received.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task HandleJsonMessage(SocketMessage message);

    /// <summary>
    /// TODO: Implement this method.
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    Task HandleBinaryData(byte[] data);
}
