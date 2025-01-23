using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using UdpClient = NetCoreServer.UdpClient;
namespace Sefirah.App.Services.Socket;

public class ServerSession(SslServer server, ITcpServerProvider socketProvider, ILogger logger) : SslSession(server)
{
    void Send(byte[] buffer, long offset, long size)
    {
        SendAsync(buffer, offset, size);
    }

    protected override void OnDisconnected()
    {
        socketProvider.OnDisconnected(this);
    }

    protected override void OnConnected()
    {
        socketProvider.OnConnected(this);
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        socketProvider.OnReceived(this, buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        logger.Error("Session {0} encountered error: {1}", Id, error);
    }
}

public class Server(SslContext context, IPAddress address, int port, ITcpServerProvider socketProvider, ILogger logger) : SslServer(context, address, port)
{
    protected override SslSession CreateSession()
    {
        logger.Debug("Creating new session");
        return new ServerSession(this, socketProvider, logger);
    }

    protected override void OnError(SocketError error)
    {
        logger.Error("Server encountered error: {0}", error);
    }
}

public class Client(SslContext context, string address, int port, ITcpClientProvider socketProvider, ILogger logger) : SslClient(context, address, port)
{
    protected override void OnConnected()
    {
        socketProvider.OnConnected();
    }

    protected override void OnHandshaking()
    {
        base.OnHandshaking();
    }

    protected override void OnDisconnected()
    {
        socketProvider.OnDisconnected();
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        socketProvider.OnReceived(buffer, offset, size);
    }

    protected override void OnError(SocketError error)
    {
        logger.Error("Client encountered error: {0}", error);
    }
}

public class MulticastServer(IPAddress address, int port) : UdpServer(address, port)
{
    protected override void OnError(SocketError error)
    {
        Debug.WriteLine($"Multicast UDP server caught an error with code {error}");
    }
}

class MulticastClient(string address, int port, IUdpClientProvider socketProvider) : UdpClient(address, port)
{

    protected override void OnConnected()
    {
        ReceiveAsync();
        socketProvider.OnConnected();
    }

    protected override void OnDisconnected()
    {
        socketProvider.OnDisconnected();
    }

    protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        socketProvider.OnReceived(endpoint, buffer, offset, size);
        ReceiveAsync();
    }
    protected override void OnError(SocketError error)
    {
        Debug.WriteLine($"Multicast UDP client caught an error with code {error}");
    }
}
