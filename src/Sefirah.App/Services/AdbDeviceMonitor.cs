using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Models;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using System.Net;
using System.Threading.Tasks;

namespace Sefirah.App.Services;

public class AdbDevice
{
    public string Serial { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DeviceState State { get; set; }
}

public interface IAdbDeviceMonitor
{
    ObservableCollection<AdbDevice> Devices { get; }
    Task StartAsync();
    Task StopAsync();
    bool IsMonitoring { get; }
}

public class AdbDeviceMonitor(
    ILogger logger
) : IAdbDeviceMonitor
{
    private CancellationTokenSource? cts;
    private DeviceMonitor? deviceMonitor;
    private readonly AdbClient adbClient = new();
    
    public ObservableCollection<AdbDevice> Devices { get; } = [];
    public bool IsMonitoring => deviceMonitor != null && !(cts?.IsCancellationRequested ?? true);

    private readonly string adbPath = "D:\\Artifacts\\scrcpy-win64-v3.1\\adb.exe";
    
    public async Task StartAsync()
    {
        if (IsMonitoring)
        {
            logger.Warn("ADB monitoring is already running");
            return;
        }
        
        cts = new CancellationTokenSource();
        
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
                        State = device.State
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
                State = fullDeviceData.State
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
                State = deviceData.State
            };
        }
    }

    public async Task Pair(string host, string pairingCode)
    {
        await adbClient.PairAsync(host, pairingCode);
    }
}
