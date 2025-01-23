namespace Sefirah.App.Data.EventArguments;

public class DiscoveredMdnsServiceArgs : EventArgs
{
    public required string ServiceInstanceName { get; set; }
    public int Port { get; set; }
}
