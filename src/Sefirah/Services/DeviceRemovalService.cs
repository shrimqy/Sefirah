using AdvancedSharpAdbClient.Models;
using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;

namespace Sefirah.Services;

public class DeviceRemovalService(
    ISessionManager sessionManager,
    IDeviceManager deviceManager,
    ISftpFeature sftpFeature,
    IAdbService adbService,
    RemoteAppRepository remoteAppRepository,
    SmsRepository smsRepository,
    ContactRepository contactRepository,
    CallLogRepository callLogRepository,
    NotificationRepository notificationRepository,
    ILogger<DeviceRemovalService> logger) : IDeviceRemovalService
{
    public Task UnpairAsync(PairedDevice device) => RemoveAsync(device, notifyDevice: true);

    public Task HandleRemoteUnpairAsync(PairedDevice device) => RemoveAsync(device, notifyDevice: false);

    private async Task RemoveAsync(PairedDevice device, bool notifyDevice)
    {
        var deviceId = device.Id;
        var deviceName = device.Name;

        if (device.IsConnected)
        {
            if (notifyDevice)
                sessionManager.Unpair(device);
            else
                sessionManager.DisconnectDevice(device, true);
        }

        await DisconnectAdbAsync(device);

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() => App.CloseDeviceSettingsWindow(deviceId));
        await deviceManager.RemoveDevice(device);
        sftpFeature.Remove(deviceId);

        await Task.Run(() =>
        {
            remoteAppRepository.RemoveAllAppsForDevice(deviceId);
            smsRepository.DeleteAllDataForDevice(deviceId);
            contactRepository.DeleteAllContactsForDevice(deviceId);
            callLogRepository.DeleteAllCallLogsForDevice(deviceId);
            notificationRepository.RemoveNotificationsForDevice(deviceId);
        });

        logger.Info($"Removed paired device {deviceName} ({(notifyDevice ? "unpaired here" : "unpaired by the device")})");
    }

    /// <summary>
    /// Turns off wireless debugging on the device and drops the ADB connection to it, unless another
    /// pairing still uses it (e.g. a second Sefirah install on the same phone). The phone warns its
    /// user that this PC's ADB key stays trusted until revoked there.
    /// </summary>
    private async Task DisconnectAdbAsync(PairedDevice device)
    {
        // Ask the ADB server rather than IAdbService.AdbDevices: that list only fills from connect
        // events, so a connection that predates this app run is missing from it
        List<DeviceData> online;
        try
        {
            online = (await adbService.AdbClient.GetDevicesAsync())
                .Where(d => d.State is DeviceState.Online)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.Warn($"Could not list ADB devices to clean up after unpairing {device.Name}", ex);
            return;
        }

        foreach (var adbDevice in online.Where(d => UsesAdbDevice(device, d)))
        {
            if (deviceManager.PairedDevices.Any(other => other != device && UsesAdbDevice(other, adbDevice)))
                continue;

            await adbService.DisableWirelessDebuggingAsync(adbDevice);
        }
    }

    /// <summary>
    /// Matches by the address in "ip:port" serials (TCP mode), otherwise by model (USB, and the
    /// mDNS-named serials of Android 11+ wireless debugging). Deliberately broad: for this cleanup,
    /// matching too much is the safe side.
    /// </summary>
    private static bool UsesAdbDevice(PairedDevice device, DeviceData adbDevice)
    {
        var serialHost = adbDevice.Serial.Contains(':') ? adbDevice.Serial.Split(':')[0] : null;
        if (serialHost is not null && System.Net.IPAddress.TryParse(serialHost, out _))
            return serialHost == device.Address || device.Addresses.Any(a => a.Address == serialHost);

        return !string.IsNullOrEmpty(adbDevice.Model) && !string.IsNullOrEmpty(device.Model) &&
            string.Equals(adbDevice.Model.Replace('_', ' ').Trim(), device.Model.Replace('_', ' ').Trim(),
                StringComparison.OrdinalIgnoreCase);
    }
}
