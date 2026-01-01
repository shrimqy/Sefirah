namespace Sefirah.Data.Models.Messages;

public class IpAddressEntry
{
    public string IpAddress { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
}

