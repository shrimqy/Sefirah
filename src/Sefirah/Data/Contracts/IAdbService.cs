using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using Sefirah.Data.Items;
using Sefirah.Data.Models;

namespace Sefirah.Data.Contracts;
public interface IAdbService
{
    ObservableCollection<AdbDevice> AdbDevices { get; }
    ObservableCollection<ScrcpyPreferenceItem> DisplayOrientationOptions { get; }
    ObservableCollection<ScrcpyPreferenceItem> GetVideoCodecOptions(string deviceModel);
    ObservableCollection<ScrcpyPreferenceItem> GetAudioCodecOptions(string deviceModel);
    Task StartAsync();
    Task<bool> ConnectWireless(string? host, int port = 5555);
    Task StopAsync();
    Task UninstallApp(string deviceId, string appPackage);
    void UnlockDevice(DeviceData deviceData, List<string> unlockCommands);
    bool IsMonitoring { get; }
    AdbClient AdbClient { get; }
    void TryConnectTcp(string host);

    Task<bool> IsLocked(DeviceData deviceData);
}
