using Sefirah.App.Data.AppDatabase.Models;

namespace Sefirah.App.Data.EventArguments;
public class ConnectedSessionEventArgs : EventArgs
{
    public bool IsConnected { get; set; }

    public string? SessionId { get; set; }

    public RemoteDeviceEntity? Device { get; set; }
}
