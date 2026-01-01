namespace Sefirah.Data.Models;

public abstract class ConnectionStatus
{
    public bool IsDisconnected => this is Disconnected;
    public bool IsConnected => this is Connected;
    public bool IsConnecting => this is Connecting;
    public bool IsForcedDisconnect => this is Disconnected disconnected && disconnected.ForcedDisconnect;
    public bool IsConnectedOrConnecting => IsConnected || IsConnecting;
}

public sealed class Connected : ConnectionStatus;

public sealed class Connecting : ConnectionStatus;

public sealed class Disconnected(bool forcedDisconnect = false) : ConnectionStatus
{
    public bool ForcedDisconnect { get; } = forcedDisconnect;
}
