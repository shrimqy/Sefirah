using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.Contracts;

public interface IDeviceManager
{
    /// <summary>
    /// Gets the device info.
    /// </summary>
    Task<RemoteDeviceEntity> GetDeviceInfoAsync(string deviceId);

    /// <summary>
    /// Gets the last connected device.
    /// </summary>
    Task<RemoteDeviceEntity?> GetLastConnectedDevice();

    /// <summary>
    /// Gets the list of devices.
    /// </summary>
    Task<List<RemoteDeviceEntity>> GetDeviceListAsync();

    /// <summary>
    /// Removes the device from the database.
    /// </summary>
    Task RemoveDevice(RemoteDeviceEntity device);

    /// <summary>
    /// Updates the device in the database.
    /// </summary>
    Task UpdateDevice(RemoteDeviceEntity device);

    /// <summary>
    /// Updates the device properties (battery..)
    /// </summary>
    Task UpdateDeviceStatus(DeviceStatus deviceStatus);

    /// <summary>
    /// Returns the device if it get's successfully verified and added to the database.
    /// </summary>
    Task<RemoteDeviceEntity?> VerifyDevice(DeviceInfo device, string? ipAddress);

    /// <summary>
    /// Event that is raised when the device properties (battery..) changes.
    /// </summary>
    event EventHandler<DeviceStatus>? DeviceStatusChanged;

    /// <summary>
    /// Gets the local device.
    /// </summary>
    Task<LocalDeviceEntity> GetLocalDeviceAsync();

    /// <summary>
    /// Gets the current device status.
    /// </summary>
    DeviceStatus? CurrentDeviceStatus { get; }

    /// <summary>
    /// Event that is raised when a device is added to the device list.
    /// </summary>
    event EventHandler<RemoteDeviceEntity>? DeviceAdded;
}