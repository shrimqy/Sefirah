using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IDeviceManager
{
    /// <summary>
    /// Gets the list of connected clients.
    /// </summary>
    ObservableCollection<PairedDevice> PairedDevices { get; }

    /// <summary>
    /// Gets or sets the currently active device session
    /// </summary>
    PairedDevice? ActiveDevice { get; set; }

    /// <summary>
    /// Finds a device session by device ID
    /// </summary>
    PairedDevice? FindDeviceById(string deviceId);

    /// <summary>
    /// Updates an existing device in the collection or adds it if it doesn't exist.
    /// This method is thread-safe and handles UI thread dispatching internally.
    /// </summary>
    void UpdateOrAddDevice(PairedDevice device, Action<PairedDevice>? updateAction = null);

    /// <summary>
    /// Gets the device info.
    /// </summary>
    Task<RemoteDeviceEntity> GetDeviceInfoAsync(string deviceId);

    /// <summary>
    /// Gets the last connected device.
    /// </summary>
    Task<PairedDevice?> GetLastConnectedDevice();

    /// <summary>
    /// Removes the device from the database.
    /// </summary>
    void RemoveDevice(PairedDevice device);

    /// <summary>
    /// Updates the device in the database.
    /// </summary>
    Task UpdateDevice(RemoteDeviceEntity device);

    /// <summary>
    /// Updates the device properties (battery..)
    /// </summary>
    void UpdateDeviceStatus(PairedDevice device, DeviceStatus deviceStatus);

    /// <summary>
    /// Returns the device if it get's successfully verified and added to the database.
    /// </summary>
    Task<PairedDevice?> VerifyDevice(DeviceInfo device, string? ipAddress);

    /// <summary>
    /// Gets the local device.
    /// </summary>
    Task<LocalDeviceEntity> GetLocalDeviceAsync();
    void UpdateLocalDevice(LocalDeviceEntity localDevice);
    Task Initialize();

    List<string> GetRemoteDeviceIpAddresses();
}
