using AdvancedSharpAdbClient.Models;

namespace Sefirah.Data.Models;
public class AdbDevice
{
    public string Serial { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string AndroidId { get; set; } = string.Empty;
    public DeviceState State { get; set; } = DeviceState.Unknown;
    public DeviceType Type { get; set; }

    public DeviceData DeviceData { get; set; }
}

public enum DeviceType
{
    USB,
    WIFI
}
