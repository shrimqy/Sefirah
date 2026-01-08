namespace Sefirah.Data.Models.Messages;

public class AddressEntry
{
    public string Address { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int Priority { get; set; } = 0;
}

