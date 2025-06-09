using System.Net;
using System.Net.Sockets;

namespace Sefirah.Services.Socket;
public interface IUdpClientProvider
{
    void OnConnected();
    void OnDisconnected();
    void OnError(SocketError error);
    void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size);
}
