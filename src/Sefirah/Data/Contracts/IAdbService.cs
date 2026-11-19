using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Sefirah.Data.Items;
using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;

public interface IAdbService
{
    ObservableCollection<AdbDevice> AdbDevices { get; }

    List<ScrcpyPreferenceItem> DisplayOrientationOptions { get; }

    ObservableCollection<ScrcpyPreferenceItem> GetVideoCodecOptions(string deviceModel);

    ObservableCollection<ScrcpyPreferenceItem> GetAudioCodecOptions(string deviceModel);

    Task StartAsync();

    Task<bool> ConnectWireless(string host, int port = 5555);

    Task StopAsync();

    Task<bool> UninstallApp(string deviceId, string appPackage);

    Task<bool> InstallAppAsync(string deviceId, string apkPath);

    void UnlockDevice(DeviceData deviceData, List<UnlockCommandEntry> unlockCommands);

    bool IsMonitoring { get; }

    AdbClient AdbClient { get; }

    Task<bool> TryConnectTcp(string host, string model);

    Task DisconnectDeviceAsync(AdbDevice device);

    /// <summary>
    /// Turns off wireless ADB on the device: the Android 11+ "Wireless debugging" toggle and TCP mode
    /// ("adb tcpip"). USB debugging stays on. Drops any wireless ADB link to it.
    /// </summary>
    Task DisableWirelessDebuggingAsync(DeviceData device);

    Task TryStartWorkerAsync(PairedDevice device, string command);

    Task<bool> IsLocked(DeviceData deviceData);
}
