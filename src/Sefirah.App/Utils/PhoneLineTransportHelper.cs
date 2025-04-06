using System.Globalization;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Sefirah.App.Utils;
public class PhoneLineTransportHelper
{
    public static readonly Guid HandsFreeProfileUuid = new("0000111f-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// Retrive a corresponding <see cref="PhoneLineTransportDevice"/> object from a <see cref="BluetoothDevice"/>
    /// </summary>
    /// <param name="bt">the bluetooth device</param>
    /// <returns>
    /// Return <see cref="PhoneLineTransportDevice"/> if the given <see cref="BluetoothDevice"/> support Hands-Free Profile. 
    /// Otherwise, return null
    /// </returns>
    public static async Task<PhoneLineTransportDevice?> GetPhoneLineTransportFromBluetoothDevice(BluetoothDevice bt)
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
        var phoneLineDevsInfo = await DeviceInformation.FindAllAsync(PhoneLineTransportDevice.GetDeviceSelector(), [deviceInterfaceBluetoothAddressKey]);
        DeviceInformation? matchPhoneLineDevInfo = phoneLineDevsInfo.Where(dev =>
        {
            string? phoneLineDevAddress = (string?)dev.Properties[deviceInterfaceBluetoothAddressKey];
            if (ulong.TryParse(phoneLineDevAddress, NumberStyles.HexNumber, null, out ulong address))
            {
                return address == bt.BluetoothAddress;
            }
            else
            {
                return false;
            }
        }).FirstOrDefault();

        if (matchPhoneLineDevInfo == null)
        {
            return null;
        }

        return PhoneLineTransportDevice.FromId(matchPhoneLineDevInfo.Id);
    }
}