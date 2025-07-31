using Sefirah.Data.Enums;

namespace Sefirah.Data.Models;

public class AppDeviceInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public NotificationFilter Filter { get; set; }
    public bool Pinned { get; set; } = false;
}
