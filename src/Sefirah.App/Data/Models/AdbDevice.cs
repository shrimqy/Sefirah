using AdvancedSharpAdbClient.Models;

namespace Sefirah.App.Data.Models;
public class AdbDevice
{
    public string Serial { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DeviceState State { get; set; }
    public DeviceType Type { get; set; }
}

public enum DeviceType
{
    Usb,
    Tcpip
}