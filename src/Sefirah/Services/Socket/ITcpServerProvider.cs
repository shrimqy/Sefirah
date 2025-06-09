using System.Net.Sockets;

namespace Sefirah.Services.Socket;

public interface ITcpServerProvider
{
    void OnConnected(ServerSession session);
    void OnDisconnected(ServerSession session);
    void OnError(SocketError error);
    void OnReceived(ServerSession session, byte[] buffer, long offset, long size);
}
