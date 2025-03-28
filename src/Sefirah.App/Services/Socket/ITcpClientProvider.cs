﻿using System.Net.Sockets;

namespace Sefirah.App.Services.Socket;
public interface ITcpClientProvider
{
    void OnConnected();
    void OnDisconnected();
    void OnError(SocketError error);
    void OnReceived(byte[] buffer, long offset, long size);
}
