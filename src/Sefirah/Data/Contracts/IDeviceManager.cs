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
    /// The list of discovered devices.
    /// </summary>
    ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; }

    /// <summary>
    /// Gets or sets the currently active device session
    /// </summary>
    PairedDevice? ActiveDevice { get; set; }

    /// <summary>
    /// Finds a device session by device ID
    /// </summary>
    PairedDevice? FindDeviceById(string deviceId);

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
    Task RemoveDevice(PairedDevice device);

    /// <summary>
    /// Updates the device in the database.
    /// </summary>
    Task UpdateDevice(RemoteDeviceEntity device);

    /// <summary>
    /// Gets the local device.
    /// </summary>
    Task<LocalDeviceEntity> GetLocalDeviceAsync();
    void UpdateLocalDevice(LocalDeviceEntity localDevice);
    Task Initialize();

    List<string> GetRemoteDeviceAddresses();

    Task UpdateDeviceInfo(PairedDevice device, DeviceInfo deviceInfo);
    Task<PairedDevice> AddDevice(DiscoveredDevice device);
}
