using System.Net;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Items;
using Sefirah.Data.Models;

namespace Sefirah.Services;

public class AdbService(
    ILogger<AdbService> logger,
    IUserSettingsService userSettingsService
) : IAdbService
{
    private CancellationTokenSource? cts;
    private DeviceMonitor? deviceMonitor;
    private readonly AdbClient adbClient = new();
    
    public ObservableCollection<AdbDevice> AdbDevices { get; } = [];
    public bool IsMonitoring => deviceMonitor != null && !(cts?.IsCancellationRequested ?? true);

    public AdbClient AdbClient => adbClient;

    // Initialize the codec option collections
    public ObservableCollection<ScrcpyPreferenceItem> DisplayOrientationOptions { get; } =
    [
        new() { Id = 0, Command = "", Display = "Default" },
        new() { Id = 1, Command = "0", Display = "0°" },
        new() { Id = 2, Command = "90", Display = "90°" },
        new() { Id = 3, Command = "180", Display = "180°" },
        new() { Id = 4, Command = "270", Display = "270°" },
        new() { Id = 5, Command = "flip0", Display = "flip-0°" },
        new() { Id = 6, Command = "flip90", Display = "flip-90°" },
        new() { Id = 7, Command = "flip180", Display = "flip-180°" },
        new() { Id = 8, Command = "flip270", Display = "flip-270°" }
    ];

    public ObservableCollection<ScrcpyPreferenceItem> VideoCodecOptions { get; } =
    [
        new() { Id = 0, Command = "", Display = "Default" },
        new() { Id = 1, Command = "--video-codec=h264 --video-encoder=OMX.qcom.video.encoder.avc", Display = "h264 & c2.qti.avc.encoder (hw)" },
        new() { Id = 2, Command = "--video-codec=h264 --video-encoder=c2.android.avc.encoder", Display = "h264 & c2.android.avc.encoder (sw)" },
        new() { Id = 4, Command = "--video-codec=h264 --video-encoder=OMX.google.h264.encoder", Display = "h264 & OMX.google.h264.encoder (sw)" },
        new() { Id = 5, Command = "--video-codec=h265 --video-encoder=OMX.qcom.video.encoder.hevc", Display = "h265 & OMX.qcom.video.encoder.hevc (hw)" },
        new() { Id = 6, Command = "--video-codec=h265 --video-encoder=c2.android.hevc.encoder", Display = "h265 & c2.android.hevc.encoder (sw)" }
    ];

    public ObservableCollection<ScrcpyPreferenceItem> AudioCodecOptions { get; } =
    [
        new() { Id = 0, Command = "", Display = "Default" },
        new() { Id = 1, Command = "--audio-codec=opus --audio-encoder=c2.android.opus.encoder", Display = "opus & c2.android.opus.encoder (sw)" },
        new() { Id = 2, Command = "--audio-codec=aac --audio-encoder=c2.android.aac.encoder", Display = "aac & c2.android.aac.encoder (sw)" },
        new() { Id = 3, Command = "--audio-codec=aac --audio-encoder=OMX.google.aac.encoder", Display = "aac & OMX.google.aac.encoder (sw)" },
        new() { Id = 4, Command = "--audio-codec=raw", Display = "raw" }
    ];

    // TODO: To add new options dynamically
    public void AddVideoCodecOption(string command, string display)
    {
        int newId = VideoCodecOptions.Count > 0 ? VideoCodecOptions.Max(x => x.Id) + 1 : 0;
        VideoCodecOptions.Add(new ScrcpyPreferenceItem { Id = newId, Command = command, Display = display });
    }

    public void AddAudioCodecOption(string command, string display)
    {
        int newId = AudioCodecOptions.Count > 0 ? AudioCodecOptions.Max(x => x.Id) + 1 : 0;
        AudioCodecOptions.Add(new ScrcpyPreferenceItem { Id = newId, Command = command, Display = display });
    }


    
    public async Task StartAsync()
    {
        try
        {
            if (IsMonitoring) return;

            cts = new CancellationTokenSource();
            string adbPath = $"{userSettingsService.GeneralSettingsService.AdbPath}";

            // Start the ADB server if it's not running
            StartServerResult startServerResult = await AdbServer.Instance.StartServerAsync(adbPath, false, cts.Token);
            logger.LogInformation($"ADB server start result: {startServerResult}");
            
            // Create and configure the device monitor
            deviceMonitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            
            deviceMonitor.DeviceConnected += DeviceConnected;
            deviceMonitor.DeviceDisconnected += DeviceDisconnected;
            deviceMonitor.DeviceChanged += DeviceChanged;

            await Task.Delay(50);
            
            await deviceMonitor.StartAsync();
            
            // Get initial list of devices
            await RefreshDevicesAsync();
            
            logger.LogInformation("ADB device monitoring started successfully");
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            logger.LogError("Failed to start ADB device monitoring: {ex}", ex);
        }
    }
    
    public async Task StopAsync()
    {
        if (!IsMonitoring)
        {
            logger.LogWarning("ADB monitoring is not running");
            return;
        }
        
        await CleanupAsync();
        logger.LogInformation("ADB device monitoring stopped");
    }
    
    private async Task CleanupAsync()
    {
        if (deviceMonitor != null)
        {
            deviceMonitor.DeviceConnected -= DeviceConnected;
            deviceMonitor.DeviceDisconnected -= DeviceDisconnected;
            deviceMonitor.DeviceChanged -= DeviceChanged;
            
            await deviceMonitor.DisposeAsync();
            deviceMonitor = null;
        }
        
        if (cts != null)
        {
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }
    }
    
    private async void DeviceConnected(object? sender, DeviceDataEventArgs e)
    {
        try
        {
            // Check if device already exists in collection
            var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == e.Device.Serial);
            if (existingDevice != null) return;

            // get the rudimentary data if it isn't online yet
            if (e.Device.State != DeviceState.Online)
            {
                logger.LogInformation($"Device {e.Device.Serial} connected but not yet online. Current state: {e.Device.State}");

                var adbDevice = new AdbDevice
                {
                    Serial = e.Device.Serial,
                    Model = e.Device.Model ?? "Unknown",
                    State = e.Device.State,
                    Type = e.Device.Serial.Contains(':') || e.Device.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB,
                    DeviceData = e.Device,
                    AndroidId = "" // Will be populated when device comes online
                };

                await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
                {
                    AdbDevices.Add(adbDevice);
                });
                return;
            }
            
            // Refresh the full device information
            var connectedDevice = await GetFullDeviceInfoAsync(e.Device);
            
            await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                AdbDevices.Add(connectedDevice);
            });
            logger.LogInformation($"Device connected: {connectedDevice.Model} ({connectedDevice.Serial})");
        }
        catch (Exception ex)
        {
            logger.LogError($"Error handling device connection for {e.Device.Serial}: {ex.Message}", ex);
        }
    }
    
    private async void DeviceDisconnected(object? sender, DeviceDataEventArgs e)
    {
        logger.LogInformation($"Device disconnected: {e.Device.Serial}");
        var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == e.Device.Serial);
        if (existingDevice != null)
        {
            await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                var index = AdbDevices.IndexOf(existingDevice);
                if (index != -1)
                {
                    AdbDevices.RemoveAt(index);
                }
            });
        }
    }
    
    private async void DeviceChanged(object? sender, DeviceDataChangeEventArgs e)
    {

        logger.LogInformation($"Device state changed: {e.Device.Serial} {e.OldState} -> {e.NewState}");
        var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == e.Device.Serial);
            
        if (e.NewState == DeviceState.Online)
        {
            var deviceInfo = await GetFullDeviceInfoAsync(e.Device);
                
            await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                if (existingDevice != null)
                {
                    // Update existing device
                    var index = AdbDevices.IndexOf(existingDevice);
                    if (index != -1)
                    {
                        AdbDevices[index] = deviceInfo;
                        logger.LogInformation($"Device updated: {deviceInfo.Model} ({deviceInfo.Serial})");
                    }
                }
                else
                {
                    // Only add if device doesn't exist
                    AdbDevices.Add(deviceInfo);
                    logger.LogInformation($"Device added: {deviceInfo.Model} ({deviceInfo.Serial})");
                }
            });
                
            logger.LogInformation($"Device connected: {deviceInfo.Model} ({deviceInfo.Serial})");
        }
        else
        {
            // Device is going offline/authorizing - just update the state if it exists
            if (existingDevice != null)
            {
                await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
                {
                    var index = AdbDevices.IndexOf(existingDevice);
                    if (index != -1)
                    {
                        existingDevice.State = e.NewState;
                        AdbDevices[index] = existingDevice;
                    }
                });
            }
        }
    }
    
    private async Task RefreshDevicesAsync()
    {
        var devices = await adbClient.GetDevicesAsync();
        if (devices.Any())
        {
            logger.LogWarning("No devices found");
            await App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
            {
                AdbDevices.Clear();
            });
            return;
        }

        await App.MainWindow!.DispatcherQueue.EnqueueAsync(async() =>
        {
            var adbDevices = new List<AdbDevice>();
            foreach (var device in devices)
            {
                AdbDevice adbDevice;
                if (device.State == DeviceState.Online)
                {
                    // Get full device info including AndroidId for online devices
                    adbDevice = await GetFullDeviceInfoAsync(device);
                }
                else
                {
                    // Create basic device info for non-online devices
                    adbDevice = new AdbDevice
                    {
                        Serial = device.Serial,
                        Model = device.Model ?? "Unknown",
                        State = device.State,
                        Type = device.Serial.Contains(':') || device.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB,
                        DeviceData = device,
                        AndroidId = ""
                    };
                }
                AdbDevices.Add(adbDevice);
            }
        });
    }
    
    private async Task<AdbDevice> GetFullDeviceInfoAsync(DeviceData deviceData)
    {
        try
        {
            // Get full device information including model
            var devices = await adbClient.GetDevicesAsync();
            var fullDeviceData = devices.FirstOrDefault(d => d.Serial == deviceData.Serial);
            string androidId = string.Empty;
            try
            {
                var androidIdReceiver = new ConsoleOutputReceiver();

                // adb shell cat /storage/emulated/0/Android/data/com.castle.sefirah/files/device_info.txt
                // Get the Android ID from the device_info.txt file since we can't directly access the android id of the App 
                // TODO: Use this for associating the adb devices with paired devices
                await adbClient.ExecuteShellCommandAsync(deviceData, "cat /storage/emulated/0/Android/data/com.castle.sefirah/files/device_info.txt", androidIdReceiver);
                androidId = androidIdReceiver.ToString().Trim();
            }
            catch (Exception ex)
            {
                logger.LogError($"Error getting Android ID for {deviceData.Serial}", ex);
            }
            logger.LogInformation($"Android ID: {androidId}");
            var device = new AdbDevice
            {
                Serial = fullDeviceData.Serial,
                Model = fullDeviceData.Model ?? "Unknown",
                AndroidId = androidId,
                State = fullDeviceData.State,
                Type = fullDeviceData.Serial.Contains(':') || fullDeviceData.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB,
                DeviceData = fullDeviceData
            };
            
            return device;
        }
        catch (Exception ex)
        {
            logger.LogError($"Error getting full device info for {deviceData.Serial}", ex);
            // Return basic information if we can't get full details
            var device = new AdbDevice
            {
                Serial = deviceData.Serial,
                Model = "Unknown",
                AndroidId = "Unknown",
                State = deviceData.State,
                Type = deviceData.Serial.Contains(':') || deviceData.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB,
                DeviceData = deviceData
            };
            
            return device;
        }
    }

    public async Task<bool> ConnectWireless(string? host, int port=5555)
    {
        if (string.IsNullOrEmpty(host)) return false;

        try
        {
            var result = await adbClient.ConnectAsync(host, port);
            logger.LogInformation($"{result}");
            if (result.Contains("failed") || result.Contains("refused"))
            {
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error connecting to default wireless device: {ex}", ex);
            return false;
        }
    }

    public async Task<bool> Pair(AdbDevice device, string pairingCode, string host, int port=5555)
    {
        if (string.IsNullOrEmpty(host)) return false;
        try
        {
            var result = await adbClient.PairAsync(host, port, pairingCode);
            logger.LogInformation($"{result}");
            if (result.Contains("failed") || result.Contains("refused"))
            {
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error connecting to wireless device {device.Serial}: {ex}", device.Serial, ex);
            return false;
        }
    }

    public async void UnlockDevice(DeviceData deviceData, List<string> commands)
    {
        try
        {
            logger.LogInformation("Unlocking device");
            if (await IsLocked(deviceData))
            {
                foreach (var command in commands)
                {
                    logger.LogInformation("Executing command: {command}", command);
                    await adbClient.ExecuteShellCommandAsync(deviceData, command);
                    await Task.Delay(250);
                }
            }
        }
        catch (Exception ex)
        { 
            logger.LogError("Error unlocking device: {ex}", ex);
        }
    }

    public async Task<bool> IsLocked(DeviceData deviceData)
    {
        ConsoleOutputReceiver consoleReceiver = new();
        await adbClient.ExecuteShellCommandAsync(deviceData, "dumpsys window policy | grep 'showing=' | cut -d '=' -f2", consoleReceiver);
        return consoleReceiver.ToString().Trim() == "true";
    }

    public async Task UninstallApp(string deviceId, string appPackage)
    {
        logger.LogInformation("Uninstalling app {appPackage} from {deviceId}", appPackage, deviceId);

        var adbDevice = AdbDevices.FirstOrDefault(d => d.AndroidId == deviceId);
        if (adbDevice?.DeviceData == null) return;
        
        var deviceData = adbDevice.DeviceData.Value;
        await adbClient.UninstallPackageAsync(deviceData, appPackage);
    }

    /// <summary>
    /// Enables TCP/IP mode by restarting ADB with tcpip 5555 command
    /// </summary>
    private async Task<bool> EnableTcpipMode()
    {
        try
        {
            string adbPath = userSettingsService.GeneralSettingsService.AdbPath;
            if (string.IsNullOrEmpty(adbPath))
            {
                logger.LogError("ADB path not configured");
                return false;
            }

            logger.LogInformation("Enabling TCP/IP mode using ADB at: {AdbPath}", adbPath);
            
            // Run "adb tcpip 5555" to enable TCP/IP mode
            var processInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = "tcpip 5555",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                logger.LogError("Failed to start ADB process");
                return false;
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            if (!string.IsNullOrEmpty(error))
            {
                logger.LogWarning("ADB tcpip command error: {Error}", error);
            }

            // Restart our ADB client to pick up the changes
            await RestartAdbClient();
            
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable TCP/IP mode");
            return false;
        }
    }

    /// <summary>
    /// Restarts the ADB client to pick up TCP/IP mode changes
    /// </summary>
    private async Task RestartAdbClient()
    {
        try
        {
            logger.LogInformation("Restarting ADB client");
            var wasMonitoring = IsMonitoring;
            if (wasMonitoring)
            {
                await CleanupAsync();
            }
            await Task.Delay(200);

            if (wasMonitoring)
            {
                await StartAsync();
            }
            logger.LogInformation("ADB client restarted successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart ADB client");
        }
    }

    public async void TryConnectTcp(string host)
    {
        try
        {
            var result = await ConnectWireless(host);
            if (result)
            {
                logger.LogInformation("Successfully connected to {Host}", host);
            }

            if (AdbDevices.FirstOrDefault(d => d.Type == DeviceType.USB) == null) return;
            
            // If connection failed, try to enable TCP/IP mode using ADB if USB is connected
            var tcpipEnabled = await EnableTcpipMode();
            if (!tcpipEnabled)
            {
                logger.LogError("Failed to enable TCP/IP mode");
                return;
            }

            await Task.Delay(200);

            // Retry the connection after enabling TCP/IP mode
            result = await ConnectWireless(host);
            if (result)
            {
                logger.LogInformation("Successfully connected to {Host} after enabling TCP/IP mode", host);
                return;
            }

            logger.LogError("TCP/IP connection still failed after enabling TCP/IP mode");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in TryConnectTcp for {Host}", host);
            return;
        }
    }
}
