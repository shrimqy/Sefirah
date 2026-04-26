using System.Globalization;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Sefirah.Platforms.Windows.Helpers;

public static class PhoneLineTransportHelper
{
    public static readonly Guid HandsFreeProfileUuid = new("0000111f-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// Returns a <see cref="PhoneLineTransportDevice"/> for the given Bluetooth device when it exposes HFP.
    /// </summary>
    public static async Task<PhoneLineTransportDevice?> GetPhoneLineTransportFromBluetoothDeviceAsync(BluetoothDevice bt)
    {
        // Check SDP first
        var result = await bt.GetRfcommServicesAsync();
        if (result.Error != BluetoothError.Success)
        {
            // Can not get any services from SDP
            return null;
        }

        var hfp = result.Services.Where(svc => svc.ServiceId.Uuid == HandsFreeProfileUuid).ToList();
        if (hfp.Count == 0)
        {
            return null;
        }

        const string deviceInterfaceBluetoothAddressKey = "System.DeviceInterface.Bluetooth.DeviceAddress";
        var phoneLineDevsInfo = await DeviceInformation.FindAllAsync(
            PhoneLineTransportDevice.GetDeviceSelector(),
            new[] { deviceInterfaceBluetoothAddressKey });

        var matchPhoneLineDevInfo = phoneLineDevsInfo.FirstOrDefault(dev =>
        {
            if (dev.Properties.ContainsKey(deviceInterfaceBluetoothAddressKey) &&
                dev.Properties[deviceInterfaceBluetoothAddressKey] is string phoneLineDevAddress &&
                ulong.TryParse(phoneLineDevAddress, NumberStyles.HexNumber, null, out var address))
            {
                return address == bt.BluetoothAddress;
            }

            return false;
        });

        if (matchPhoneLineDevInfo == null)
        {
            return null;
        }

        return PhoneLineTransportDevice.FromId(matchPhoneLineDevInfo.Id);
    }
}
