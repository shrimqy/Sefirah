using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;

public interface IDeviceManager
{
    Task<RemoteDeviceEntity> GetDeviceInfoAsync(string deviceId);
    Task<RemoteDeviceEntity?> GetLastConnectedDevice();
    Task<List<RemoteDeviceEntity>> GetDeviceListAsync();
    Task RemoveDevice(RemoteDeviceEntity device);
    Task UpdateDevice(RemoteDeviceEntity device);

    /// <summary>
    /// Updates the device status.
    /// </summary>
    Task UpdateDeviceStatus(DeviceStatus deviceStatus);

    /// <summary>
    /// Returns the device if it get's successfully verified and added to the database.
    /// </summary>
    Task<RemoteDeviceEntity?> VerifyDevice(DeviceInfo device);

    event EventHandler<DeviceStatus>? DeviceStatusChanged;

    Task<LocalDeviceEntity> GetLocalDeviceAsync();
}