namespace Sefirah.App.Data.Contracts;
public interface INetworkService
{
    /// <summary>
    /// Starts the socket server.
    /// </summary>
    /// <returns>True if server started successfully, false otherwise.</returns>
    Task<bool> StartServerAsync();
}
