using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;
using System.Net;

namespace Sefirah.App.Services;

public interface IAdbService
{
    ObservableCollection<AdbDevice> Devices { get; }
    Task StartAsync();
    Task<bool> ConnectWireless(string host, int port=5555);
    Task StopAsync();
    bool IsMonitoring { get; }
}

public class AdbService(
    ILogger logger,
    IUserSettingsService userSettingsService
) : IAdbService
{
    private CancellationTokenSource? cts;
    private DeviceMonitor? deviceMonitor;
    private readonly AdbClient adbClient = new();
    
    public ObservableCollection<AdbDevice> Devices { get; } = [];
    public bool IsMonitoring => deviceMonitor != null && !(cts?.IsCancellationRequested ?? true);

    public async Task StartAsync()
    {
        if (IsMonitoring)
        {
            logger.Warn("ADB monitoring is already running");
            return;
        }
        
        cts = new CancellationTokenSource();
        string adbPath = $"{userSettingsService.FeatureSettingsService.ScrcpyPath}\\adb.exe";
        try
        {
            // Start the ADB server if it's not running
            StartServerResult startServerResult = await AdbServer.Instance.StartServerAsync(adbPath, false, cts.Token);
            logger.Info($"ADB server start result: {startServerResult}");
            
            // Create and configure the device monitor
            deviceMonitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            
            // Configure event handlers
            deviceMonitor.DeviceConnected += DeviceConnected;
            deviceMonitor.DeviceDisconnected += DeviceDisconnected;
            deviceMonitor.DeviceChanged += DeviceChanged;
            
            // Start monitoring devices
            await deviceMonitor.StartAsync();
            
            // Get initial list of devices
            await RefreshDevicesAsync();
            
            logger.Info("ADB device monitoring started successfully");
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            logger.Error("Failed to start ADB device monitoring", ex);
            throw;
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
            // Unregister event handlers
            deviceMonitor.DeviceConnected -= DeviceConnected;
            deviceMonitor.DeviceDisconnected -= DeviceDisconnected;
            deviceMonitor.DeviceChanged -= DeviceChanged;
            
            // Stop and dispose the monitor
            await deviceMonitor.DisposeAsync();
            deviceMonitor = null;
        }
        
        // Cancel and dispose the token source
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
            // Refresh the full device information
            var connectedDevice = await GetFullDeviceInfoAsync(e.Device);
            Devices.Add(connectedDevice);
            logger.Info($"Device connected: {connectedDevice.Model} ({connectedDevice.Serial})");
            
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling device connection for {e.Device.Serial}", ex);
        }
    }
    
    private async void DeviceDisconnected(object? sender, DeviceDataEventArgs e)
    {
        try
        {
            logger.Info($"Device disconnected: {e.Device.Serial}");
            
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
            var index = Devices.IndexOf(Devices.FirstOrDefault(d => d.Serial == e.Device.Serial));
            if (index != -1)
            {
                Devices[index].State = e.NewState;
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
                Devices.Clear();
                return;
            }
            // Convert to our AdbDevice model and add to collection
            await DispatcherQueue.GetForCurrentThread().EnqueueAsync(() =>
            {
                Devices.Clear();
                foreach (var device in devices)
                {
                    var adbDevice = new AdbDevice
                    {
                        Serial = device.Serial,
                        Model = device.Model ?? "Unknown",
                        State = device.State,
                        Type = device.Serial.Contains(':') ? DeviceType.Tcpip : DeviceType.Usb
                    };
                    Devices.Add(adbDevice);
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
            return new AdbDevice
            {
                Serial = fullDeviceData.Serial,
                Model = fullDeviceData.Model ?? "Unknown",
                State = fullDeviceData.State,
                Type = fullDeviceData.Serial.Contains(':') ? DeviceType.Tcpip : DeviceType.Usb
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
                State = deviceData.State,
                Type = deviceData.Serial.Contains(':') ? DeviceType.Tcpip : DeviceType.Usb
            };
        }
    }

    public async Task<bool> ConnectWireless(string host, int port=5555)
    {
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
