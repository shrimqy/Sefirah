using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using UdpClient = NetCoreServer.UdpClient;

namespace Sefirah.Services.Socket;

public partial class ServerSession(SslServer server, ITcpServerProvider socketProvider, ILogger logger) : SslSession(server)
{

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
        logger.LogError("Session {Id} encountered error: {error}", Id, error);
    }
}

public partial class Server(SslContext context, IPAddress address, int port, ITcpServerProvider socketProvider, ILogger logger) : SslServer(context, address, port)
{
    protected override SslSession CreateSession()
    {
        logger.LogDebug("Creating new session");
        return new ServerSession(this, socketProvider, logger);
    }

    protected override void OnError(SocketError error)
    {
        logger.LogError("Session {Id} encountered error: {error}", Id, error);
    }
}

public partial class Client(SslContext context, string address, int port, ITcpClientProvider socketProvider, ILogger logger) : SslClient(context, address, port)
{
    protected override void OnConnected()
    {
        socketProvider.OnConnected();
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
        logger.LogError("Session {Id} encountered error: {error}", Id, error);
    }
}


public partial class MulticastClient(string address, int port, IUdpClientProvider socketProvider, ILogger logger) : UdpClient(address, port)
{

    protected override void OnConnected()
    {
        ReceiveAsync();
    }

    protected override void OnDisconnected()
    {
    }

    protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size)
    {
        socketProvider.OnReceived(endpoint, buffer, offset, size);
        ReceiveAsync();
    }
    protected override void OnError(SocketError error)
    {
        logger.LogError("Session {Id} encountered error: {error}", Id, error);
    }
}
