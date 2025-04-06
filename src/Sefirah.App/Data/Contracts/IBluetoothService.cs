using Windows.Devices.Enumeration;

namespace Sefirah.App.Data.Contracts;
public interface IBluetoothService
{
    void CreateDeviceWatcher();

    Task<DevicePairingResult> PairDeviceAsync(DeviceInformation deviceInformation);
    ObservableCollection<DeviceInformation> BluetoothDevices { get; }
}
