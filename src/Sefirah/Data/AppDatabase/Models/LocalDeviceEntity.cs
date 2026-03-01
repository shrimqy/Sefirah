using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class LocalDeviceEntity
{
    [PrimaryKey]
    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;
}
