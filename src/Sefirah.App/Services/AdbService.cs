using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Items;
using Sefirah.App.Data.Models;
using System.Net;

namespace Sefirah.App.Services;

public interface IAdbService
{
    ObservableCollection<AdbDevice> AdbDevices { get; }
    ObservableCollection<ScrcpyPreferenceItem> DisplayOrientationOptions { get; }
    ObservableCollection<ScrcpyPreferenceItem> VideoCodecOptions { get; }
    ObservableCollection<ScrcpyPreferenceItem> AudioCodecOptions { get; }
    Task StartAsync();
    Task<bool> ConnectWireless(string? host, int port=5555);
    Task StopAsync();
    bool IsMonitoring { get; }
}

public class AdbService(
    ILogger logger,
    IDeviceManager deviceManager,
    IUserSettingsService userSettingsService
) : IAdbService
{
    private CancellationTokenSource? cts;
    private DeviceMonitor? deviceMonitor;
    private readonly AdbClient adbClient = new();
    
    public ObservableCollection<AdbDevice> AdbDevices { get; } = [];
    public bool IsMonitoring => deviceMonitor != null && !(cts?.IsCancellationRequested ?? true);

    // Initialize the codec option collections
    public ObservableCollection<ScrcpyPreferenceItem> DisplayOrientationOptions { get; } = new()
    {
        new() { Id = 0, Command = "", Display = "Default" },
        new() { Id = 1, Command = "0", Display = "0°" },
        new() { Id = 2, Command = "90", Display = "90°" },
        new() { Id = 3, Command = "180", Display = "180°" },
        new() { Id = 4, Command = "270", Display = "270°" },
        new() { Id = 5, Command = "flip0", Display = "flip-0°" },
        new() { Id = 6, Command = "flip90", Display = "flip-90°" },
        new() { Id = 7, Command = "flip180", Display = "flip-180°" },
        new() { Id = 8, Command = "flip270", Display = "flip-270°" }
    };

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
            if (IsMonitoring)
            {
                logger.Warn("ADB monitoring is already running");
                return;
            }

            cts = new CancellationTokenSource();
            string adbPath = $"{userSettingsService.FeatureSettingsService.AdbPath}";

            // Start the ADB server if it's not running
            StartServerResult startServerResult = await AdbServer.Instance.StartServerAsync(adbPath, false, cts.Token);
            logger.Info($"ADB server start result: {startServerResult}");
            
            // Create and configure the device monitor
            deviceMonitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            
            deviceMonitor.DeviceConnected += DeviceConnected;
            deviceMonitor.DeviceDisconnected += DeviceDisconnected;
            deviceMonitor.DeviceChanged += DeviceChanged;
            
            await deviceMonitor.StartAsync();
            
            // Get initial list of devices
            await RefreshDevicesAsync();
            
            logger.Info("ADB device monitoring started successfully");
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            logger.Error("Failed to start ADB device monitoring", ex);
        }
    }
    
    public async Task StopAsync()
    {
        if (!IsMonitoring)
        {
            logger.Warn("ADB monitoring is not running");
            return;
        }
        
        await CleanupAsync();
        logger.Info("ADB device monitoring stopped");
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

            var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == e.Device.Serial);
            if (existingDevice != null) return;

            // get the rudimentary data if it isn't online yet
            if (e.Device.State != DeviceState.Online)
            {
                logger.Info($"Device {e.Device.Serial} connected but not yet online. Current state: {e.Device.State}");

                var adbDevice = new AdbDevice
                {
                    Serial = e.Device.Serial,
                    Model = e.Device.Model ?? "Unknown",
                    State = e.Device.State,
                    Type = e.Device.Serial.Contains(':') || e.Device.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB
                };
                AdbDevices.Add(adbDevice);
                return;
            }
            
            // Refresh the full device information
            var connectedDevice = await GetFullDeviceInfoAsync(e.Device);
            AdbDevices.Add(connectedDevice);
            logger.Info($"Device connected: {connectedDevice.Model} ({connectedDevice.Serial})");
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling device connection for {e.Device.Serial}", ex);
        }
    }
    
    private void DeviceDisconnected(object? sender, DeviceDataEventArgs e)
    {
        try
        {
            logger.Info($"Device disconnected: {e.Device.Serial}");
            
            var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == e.Device.Serial);
            if (existingDevice != null)
            {
                var index = AdbDevices.IndexOf(existingDevice);
                if (index != -1)
                {
                    AdbDevices.RemoveAt(index);
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling device disconnection for {e.Device.Serial}", ex);
        }
    }
    
    private async void DeviceChanged(object? sender, DeviceDataChangeEventArgs e)
    {
        try
        {
            logger.Info($"Device state changed: {e.Device.Serial} {e.OldState} -> {e.NewState}");
            var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == e.Device.Serial);
            if (existingDevice == null)
            {
                logger.Warn($"Device {e.Device.Serial} not found in device list");
                return;
            }
            var index = AdbDevices.IndexOf(existingDevice);
            
            if (e.NewState == DeviceState.Online)
            {
                var deviceInfo = await GetFullDeviceInfoAsync(e.Device);
                
                if (index != -1)
                {
                    AdbDevices[index] = deviceInfo;
                    logger.Info($"Device updated: {deviceInfo.Model} ({deviceInfo.Serial})");
                }

                else
                {
                    AdbDevices.Add(deviceInfo);
                }
                
                logger.Info($"Device connected: {deviceInfo.Model} ({deviceInfo.Serial})");
            }
            else if (index != -1)
            {
                existingDevice.State = e.NewState;
                AdbDevices[index] = existingDevice;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling device state change for {e.Device.Serial}", ex);
        }
    }
    
    private async Task RefreshDevicesAsync()
    {
        try
        {
            var devices = await adbClient.GetDevicesAsync();
            if (!devices.Any())
            {
                logger.Warn("No devices found");
                AdbDevices.Clear();
                return;
            }
            // Convert to our AdbDevice model and add to collection
            await DispatcherQueue.GetForCurrentThread().EnqueueAsync(() =>
            {
                AdbDevices.Clear();
                foreach (var device in devices)
                {
                    var adbDevice = new AdbDevice
                    {
                        Serial = device.Serial,
                        Model = device.Model ?? "Unknown",
                        State = device.State,
                        Type = device.Serial.Contains(':') || device.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB
                    };
                    AdbDevices.Add(adbDevice);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error("Error refreshing device list", ex);
        }
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
                logger.Error($"Error getting Android ID for {deviceData.Serial}", ex);
            }
            logger.Info($"Android ID: {androidId}");
            return new AdbDevice
            {
                Serial = fullDeviceData.Serial,
                Model = fullDeviceData.Model ?? "Unknown",
                AndroidId = androidId,
                State = fullDeviceData.State,
                Type = fullDeviceData.Serial.Contains(':') || fullDeviceData.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB
            };
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting full device info for {deviceData.Serial}", ex);
            // Return basic information if we can't get full details
            return new AdbDevice
            {
                Serial = deviceData.Serial,
                Model = "Unknown",
                AndroidId = "Unknown",
                State = deviceData.State,
                Type = deviceData.Serial.Contains(':') || deviceData.Serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB
            };
        }
    }

    public async Task<bool> ConnectWireless(string? host, int port=5555)
    {
        if (string.IsNullOrEmpty(host)) return false;

        try
        {
            var result = await adbClient.ConnectAsync(host, port);
            logger.Info($"{result}");
            if (result.Contains("failed") || result.Contains("refused"))
            {
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Error connecting to default wireless device", ex);
            return false;
        }
    }

    public async Task Pair(string host, string pairingCode)
    {
        await adbClient.PairAsync(host, pairingCode);
    }
}
