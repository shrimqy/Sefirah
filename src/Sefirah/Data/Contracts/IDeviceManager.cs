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
    /// Event raised when the active device changes.
    /// </summary>
    event EventHandler<PairedDevice?>? ActiveDeviceChanged;

    /// <summary>
    /// Finds a device session by device ID
    /// </summary>
    PairedDevice? FindDeviceById(string deviceId);

    /// <summary>
    /// Finds a paired device by its Windows phone-line transport device ID.
    /// </summary>
    PairedDevice? FindDeviceByTransportId(string transportDeviceId);

    /// <summary>
    /// Gets the device info.
    /// </summary>
    Task<PairedDeviceEntity> GetPairedDevice(string deviceId);

    /// <summary>
    /// Removes the device from the database.
    /// </summary>
    Task RemoveDevice(PairedDevice device);

    /// <summary>
    /// Persists the paired device's current state to the database.
    /// </summary>
    Task UpdateDevice(PairedDevice device);

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
