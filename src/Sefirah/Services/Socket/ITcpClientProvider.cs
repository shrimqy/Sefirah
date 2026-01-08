using System.Net.Sockets;

namespace Sefirah.Services.Socket;
public interface ITcpClientProvider
{
    void OnConnected(Client client);
    void OnDisconnected(Client client);
    void OnError(Client client, SocketError error);
    void OnReceived(Client client, byte[] buffer, long offset, long size);
    void OnHandshaked(Client client);
}
