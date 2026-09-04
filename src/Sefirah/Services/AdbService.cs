using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using CommunityToolkit.WinUI;
using Sefirah.Data.Items;
using Sefirah.Data.Models;

namespace Sefirah.Services;

public class AdbService(
    ILogger<AdbService> logger,
    IDeviceManager deviceManager,
    IUserSettingsService userSettingsService
) : IAdbService
{
    private CancellationTokenSource? cts;
    private DeviceMonitor? deviceMonitor;
    private readonly AdbClient adbClient = new();
    
    public ObservableCollection<AdbDevice> AdbDevices { get; } = [];
    public bool IsMonitoring => deviceMonitor is not null && !(cts?.IsCancellationRequested ?? true);

    private static readonly string DEFAULT = "Default".GetLocalizedResource();

    private const string SefirahAndroidPackageId = "com.castle.sefirah";
    private const string WorkerNiceName = "sefirah_worker";

    public AdbClient AdbClient => adbClient;

    public List<ScrcpyPreferenceItem> DisplayOrientationOptions { get; } =
    [
        new("", DEFAULT),
        new("0", "0°"),
        new("90", "90°"),
        new("180", "180°"),
        new("270", "270°"),
        new("flip0", "flip-0°"),
        new("flip90", "flip-90°"),
        new("flip180", "flip-180°"),
        new("flip270", "flip-270°")
    ];

    public Dictionary<string, ObservableCollection<ScrcpyPreferenceItem>> VideoCodecOptions { get; } = [];
    public Dictionary<string, ObservableCollection<ScrcpyPreferenceItem>> AudioCodecOptions { get; } = [];
    private readonly Lock _codecLock = new();

    public ObservableCollection<ScrcpyPreferenceItem> GetVideoCodecOptions(string deviceModel)
    {
        if (!VideoCodecOptions.TryGetValue(deviceModel, out ObservableCollection<ScrcpyPreferenceItem>? value))
        {
            value = [new("", DEFAULT)];
            VideoCodecOptions[deviceModel] = value;
        }
        return value;
    }

    public ObservableCollection<ScrcpyPreferenceItem> GetAudioCodecOptions(string deviceModel)
    {
        if (!AudioCodecOptions.TryGetValue(deviceModel, out ObservableCollection<ScrcpyPreferenceItem>? value))
        {
            value =
            [
                new("", DEFAULT)
            ];
            AudioCodecOptions[deviceModel] = value;
        }

        return value;
    }

    private void AddVideoCodecOption(string deviceModel, string command, string display)
    {
        lock (_codecLock)
        {
            var options = GetVideoCodecOptions(deviceModel);
            var existingCommands = options.Select(v => v.Command).ToHashSet();

            if (!existingCommands.Contains(command))
                options.Add(new ScrcpyPreferenceItem(command, display));
        }
    }

    private void AddAudioCodecOption(string deviceModel, string command, string display)
    {
        lock (_codecLock)
        {
            var options = GetAudioCodecOptions(deviceModel);
            var existingCommands = options.Select(a => a.Command).ToHashSet();

            if (!existingCommands.Contains(command))
                options.Add(new ScrcpyPreferenceItem(command, display));
        }
    }

    public async Task StartAsync()
    {
        try
        {
            var adbPath = userSettingsService.GeneralSettingsService.AdbPath;
            if (IsMonitoring || string.IsNullOrEmpty(adbPath)) return;

            cts = new CancellationTokenSource();

            StartServerResult startServerResult = await AdbServer.Instance.StartServerAsync(adbPath, false, cts.Token);

            deviceMonitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));

            // Get initial list of devices
            await RefreshDevicesAsync();

            deviceMonitor.DeviceConnected += DeviceConnected;
            deviceMonitor.DeviceDisconnected += DeviceDisconnected;
            deviceMonitor.DeviceChanged += DeviceChanged;
            
            await deviceMonitor.StartAsync();
            logger.Info("ADB device monitoring started successfully");
        }
        catch (Exception ex)
        {
            await CleanupAsync();
            logger.Error($"Failed to start ADB device monitoring: {ex.Message}", ex);
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
        if (deviceMonitor is not null)
        {
            deviceMonitor.DeviceConnected -= DeviceConnected;
            deviceMonitor.DeviceDisconnected -= DeviceDisconnected;
            deviceMonitor.DeviceChanged -= DeviceChanged;
            
            await deviceMonitor.DisposeAsync();
            deviceMonitor = null;
        }
        
        if (cts is not null)
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
            var serial = e.Device.Serial;

            // get the rudimentary data if it isn't online yet
            if (e.Device.State is not DeviceState.Online)
            {
                logger.Info($"Device {serial} connected but not yet online. Current state: {e.Device.State}");

                var adbDevice = new AdbDevice
                {
                    Serial = serial,
                    Model = e.Device.Model ?? "Unknown",
                    State = e.Device.State,
                    Type = serial.Contains(':') || serial.Contains("tcp") ? DeviceType.WIFI : DeviceType.USB,
                    DeviceData = e.Device,
                    AndroidId = ""
                };

                await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                {
                    var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == serial);
                    if (existingDevice != null) return;
                    AdbDevices.Add(adbDevice);
                });
                return;
            }
            // Refresh the full device information
            var connectedDevice = await GetFullDeviceInfoAsync(e.Device);

            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == serial);
                if (existingDevice != null) return;
                AdbDevices.Add(connectedDevice);
            });
            _ = Task.Run(async () => await DiscoverCodecOptionsForDevice(connectedDevice));
            _ = Task.Run(async () => await GrantSensitiveNotificationAsync(connectedDevice));
            if (connectedDevice.Type is DeviceType.USB)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    await AutoSetupWirelessAdbAsync(connectedDevice);
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling device connection for {e.Device.Serial}: {ex.Message}", ex);
        }
    }
    
    private async void DeviceDisconnected(object? sender, DeviceDataEventArgs e)
    {
        var serial = e.Device.Serial;
        logger.Info($"Device disconnected: {serial}");

        await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == serial);
            if (existingDevice == null) return;

            var index = AdbDevices.IndexOf(existingDevice);
            if (index != -1)
                AdbDevices.RemoveAt(index);
        });
    }
    
    private async void DeviceChanged(object? sender, DeviceDataChangeEventArgs e)
    {
        var serial = e.Device.Serial;
        logger.Info($"Device state changed: {serial} {e.OldState} -> {e.NewState}");

        if (e.NewState is DeviceState.Online)
        {
            var deviceInfo = await GetFullDeviceInfoAsync(e.Device);

            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == serial);
                if (existingDevice != null)
                {
                    // Update existing device
                    var index = AdbDevices.IndexOf(existingDevice);
                    if (index != -1)
                    {
                        AdbDevices[index] = deviceInfo;
                        logger.Info($"Device updated: {deviceInfo.Model} ({deviceInfo.Serial})");
                    }
                }
                else
                {
                    // Only add if device doesn't exist
                    AdbDevices.Add(deviceInfo);
                    logger.Info($"Device added: {deviceInfo.Model} ({deviceInfo.Serial})");
                }
            });

            logger.Info($"Device connected: {deviceInfo.Model} ({deviceInfo.Serial})");

            _ = Task.Run(async () => await DiscoverCodecOptionsForDevice(deviceInfo));
            _ = Task.Run(async () => await GrantSensitiveNotificationAsync(deviceInfo));
            if (deviceInfo.Type is DeviceType.USB)
            {
                _ = Task.Run(async () => await AutoSetupWirelessAdbAsync(deviceInfo));
            }
        }
        else
        {
            // Device is going offline/authorizing - just update the state if it exists
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                var existingDevice = AdbDevices.FirstOrDefault(d => d.Serial == serial);
                if (existingDevice == null) return;

                var index = AdbDevices.IndexOf(existingDevice);
                if (index != -1)
                {
                    existingDevice.State = e.NewState;
                    existingDevice.DeviceData = e.Device;
                    AdbDevices[index] = existingDevice;
                }
            });
        }
    }
    
    private async Task RefreshDevicesAsync()
    {
        var devices = await adbClient.GetDevicesAsync();
        if (!devices.Any())
        {
            logger.Warn("No adb devices found");
            return;
        }

        await App.MainWindow.DispatcherQueue.EnqueueAsync(async() =>
        {
            var adbDevices = new List<AdbDevice>();
            foreach (var device in devices)
            {
                AdbDevice adbDevice;
                if (device.State is DeviceState.Online)
                {
                    // Get full device info including AndroidId for online devices
                    adbDevice = await GetFullDeviceInfoAsync(device);
                    AdbDevices.Add(adbDevice);
                    
                    // Discover codec options for this device
                    _ = Task.Run(async () => await DiscoverCodecOptionsForDevice(adbDevice));
                    _ = Task.Run(async () => await GrantSensitiveNotificationAsync(adbDevice));
                    if (adbDevice.Type is DeviceType.USB)
                    {
                        _ = Task.Run(async () => await AutoSetupWirelessAdbAsync(adbDevice));
                    }
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
                    AdbDevices.Add(adbDevice);
                }
            }
        });
    }
    
    private async Task<AdbDevice> GetFullDeviceInfoAsync(DeviceData deviceData)
    {
        try
        {
            // Get full device information including model
            var devices = await adbClient.GetDevicesAsync();
            var fullDeviceData = devices.FirstOrDefault(d => d.Serial == deviceData.Serial) 
                ?? throw new Exception($"Device {deviceData.Serial} not found in device list");

            string androidId = string.Empty;
            try
            {
                var androidIdReceiver = new ConsoleOutputReceiver();

                // adb shell cat /storage/emulated/0/Android/data/com.castle.sefirah/files/device_info.txt
                // Get the Android ID from the device_info.txt file since we can't directly access the android id of the App 
                await adbClient.ExecuteShellCommandAsync(deviceData, "cat /storage/emulated/0/Android/data/com.castle.sefirah/files/device_info.txt", androidIdReceiver);
                var id = androidIdReceiver.ToString().Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    // Extract the Android ID from the output
                    androidId = id;
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error getting Android ID for {deviceData.Serial}", ex);
            }

            // Look for paired devices with matching IP or model
            if (string.IsNullOrEmpty(androidId))
            {
                var deviceModel = fullDeviceData.Model;
                var pairedDevices = deviceManager.PairedDevices;

                // First try matching by IP prefix (for Wi-Fi ADB devices where Serial is <IP>:<PORT>)
                var matchingDevice = pairedDevices.FirstOrDefault(pd =>
                    (!string.IsNullOrEmpty(pd.Address) && fullDeviceData.Serial.StartsWith(pd.Address)) ||
                    pd.Addresses.Any(a => !string.IsNullOrEmpty(a.Address) && fullDeviceData.Serial.StartsWith(a.Address)));

                // Fall back to normalized model matching
                if (matchingDevice is null && !string.IsNullOrEmpty(deviceModel))
                {
                    var cleanAdbModel = deviceModel.Replace('_', ' ').Trim();
                    matchingDevice = pairedDevices.FirstOrDefault(pd =>
                    {
                        if (string.IsNullOrEmpty(pd.Model)) return false;
                        var cleanPdModel = pd.Model.Replace('_', ' ').Trim();
                        return cleanPdModel.Equals(cleanAdbModel, StringComparison.OrdinalIgnoreCase) ||
                               cleanPdModel.Contains(cleanAdbModel, StringComparison.OrdinalIgnoreCase) ||
                               cleanAdbModel.Contains(cleanPdModel, StringComparison.OrdinalIgnoreCase);
                    });
                }

                if (matchingDevice is not null)
                {
                    androidId = matchingDevice.Id;
                }
                else
                {
                    logger.Warn($"No matching paired device found for model: {deviceModel}");
                    androidId = string.Empty;
                }
            }

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
            logger.Error($"Error getting full device info for {deviceData.Serial}", ex);
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

    private async Task DiscoverCodecOptionsForDevice(AdbDevice device)
    {
        try
        {
            if (device.DeviceData is null) return;

            // Check if scrcpy is configured
            var scrcpyPath = userSettingsService.GeneralSettingsService.ScrcpyPath;
            if (string.IsNullOrEmpty(scrcpyPath) || !File.Exists(scrcpyPath))
            {
                logger.Info("Scrcpy path not configured or not found, skipping codec discovery");
                return;
            }

            var deviceModel = device.Model ?? "Unknown";

            // Run scrcpy --list-encoders
            var processInfo = new ProcessStartInfo
            {
                FileName = scrcpyPath,
                Arguments = $"--list-encoders -s {device.DeviceData.Serial}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            if (process.ExitCode != 0)
            {
                logger.Warn($"scrcpy --list-encoders failed for {deviceModel} with exit code {process.ExitCode}. Error: {error}");
                return;
            }

            ParseEncoderOutput(output, deviceModel);
        }
        catch (Exception ex)
        {
            logger.Error($"Error discovering codec options for device {device.Serial}: {ex.Message}", ex);
        }
    }


    private static readonly char[] separator = ['\r', '\n'];

    private void ParseEncoderOutput(string output, string deviceModel)
    {
        if (string.IsNullOrWhiteSpace(output))
            return;

        var lines = output.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.Contains("--video-codec", StringComparison.OrdinalIgnoreCase))
            {
                ParseVideoEncoder(trimmedLine, deviceModel);
            }
            else if (trimmedLine.Contains("--audio-codec", StringComparison.OrdinalIgnoreCase))
            {
                ParseAudioEncoder(trimmedLine, deviceModel);
            }
        }
    }

    private void ParseVideoEncoder(string line, string deviceModel)
    {
        var commandEndIndex = line.IndexOf('(');
        var command = line[..commandEndIndex].Trim();

        // Extract codec and encoder for display name
        var codecMatch = Regex.Match(command, @"--video-codec=(\w+)");
        var encoderMatch = Regex.Match(command, @"--video-encoder=([^\s]+)");

        string display;

        var codec = codecMatch.Groups[1].Value;
        var encoder = encoderMatch.Groups[1].Value;
        display = $"{codec} & {encoder}";

        // Add hardware/software indicator
        if (line.Contains("(hw)", StringComparison.OrdinalIgnoreCase))
            display += " (hw)";
        else if (line.Contains("(sw)", StringComparison.OrdinalIgnoreCase))
            display += " (sw)";

        AddVideoCodecOption(deviceModel, command, display);
    }

    private void ParseAudioEncoder(string line, string deviceModel)
    {
        var commandEndIndex = line.IndexOf('(');
        var command = line[..commandEndIndex].Trim();

        // Extract codec and encoder for display name
        var codecMatch = Regex.Match(command, @"--audio-codec=(\w+)");
        var encoderMatch = Regex.Match(command, @"--audio-encoder=([^\s]+)");

        string display;

        var codec = codecMatch.Groups[1].Value;
        var encoder = encoderMatch.Groups[1].Value;
        display = $"{codec} & {encoder}";

        // Add hardware/software indicator
        if (line.Contains("(hw)", StringComparison.OrdinalIgnoreCase))
            display += " (hw)";
        else if (line.Contains("(sw)", StringComparison.OrdinalIgnoreCase))
            display += " (sw)";

        AddAudioCodecOption(deviceModel, command, display);
    }

    /// <summary>
    /// Parses <c>adb connect</c> / <c>adb pair</c> text output. Must not rely on English-only words like
    /// "refused" because ADB prints localized reasons (e.g. Polish) after an English "cannot connect" prefix.
    /// </summary>
    private static bool IsAdbConnectOrPairSuccess(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        var m = message.Trim();
        if (m.Contains("cannot connect", StringComparison.OrdinalIgnoreCase)) return false;
        if (m.Contains("cannot resolve", StringComparison.OrdinalIgnoreCase)) return false;
        if (m.Contains("failed to authenticate", StringComparison.OrdinalIgnoreCase)) return false;
        if (m.Contains("failed to connect", StringComparison.OrdinalIgnoreCase)) return false;
        if (m.Contains("unable to connect", StringComparison.OrdinalIgnoreCase)) return false;
        return m.Contains("connected to", StringComparison.OrdinalIgnoreCase)
            || m.Contains("already connected to", StringComparison.OrdinalIgnoreCase)
            || m.Contains("successfully paired", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> ConnectWireless(string host, int port=5555)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        var cleanHost = host.Trim();
        if (cleanHost.Contains(':'))
        {
            var parts = cleanHost.Split(':');
            cleanHost = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedPort))
            {
                port = parsedPort;
            }
        }

        try
        {
            var result = await adbClient.ConnectAsync(cleanHost, port);
            logger.Info($"adb wireless connection ({cleanHost}:{port}): {result}");
            return IsAdbConnectOrPairSuccess(result);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.ConnectionRefused)
        {
            logger.Debug($"ADB server is not accepting connections while connecting wireless device {cleanHost}:{port}");
            return false;
        }
        catch (Exception ex)
        {
            logger.Error($"Error connecting to default wireless device: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> Pair(AdbDevice device, string pairingCode, string host, int port=5555)
    {
        if (string.IsNullOrEmpty(host)) return false;
        try
        {
            var result = await adbClient.PairAsync(host, port, pairingCode);
            return IsAdbConnectOrPairSuccess(result);
        }
        catch (Exception ex)
        {
            logger.Error($"Error connecting to wireless device {device.Serial}: {ex.Message}", ex);
            return false;
        }
    }

    public async void UnlockDevice(DeviceData deviceData, List<UnlockCommandEntry> commands)
    {
        try
        {
            logger.Info("Unlocking device");
            foreach (var entry in commands)
            {
                if (string.IsNullOrWhiteSpace(entry.Command))
                    continue;

                logger.Info($"Executing command: {entry.Command}, delayed {entry.DelayMs}");
                await adbClient.ExecuteShellCommandAsync(deviceData, entry.Command);
                if (entry.DelayMs > 0)
                    await Task.Delay(entry.DelayMs);
            }
        }
        catch (Exception ex)
        { 
            logger.Error($"Error unlocking device: {ex.Message}", ex);
        }
    }

    public async Task<bool> IsLocked(DeviceData deviceData)
    {
        ConsoleOutputReceiver consoleReceiver = new();
        await adbClient.ExecuteShellCommandAsync(deviceData, "dumpsys window policy | grep 'showing=' | cut -d '=' -f2", consoleReceiver);
        return consoleReceiver.ToString().Trim() == "true";
    }

    public async Task<bool> UninstallApp(string deviceId, string appPackage)
    {
        logger.Info($"Uninstalling app {appPackage} from {deviceId}");

        var adbDevice = GetOnlineAdbDevice(deviceId);
        if (adbDevice?.DeviceData is null) return false;
        
        await adbClient.UninstallPackageAsync(adbDevice.DeviceData, appPackage);
        return true;
    }

    public async Task<bool> InstallAppAsync(string deviceId, string apkPath)
    {
        logger.Info($"Installing app from {apkPath} on {deviceId}");

        var adbDevice = GetOnlineAdbDevice(deviceId);
        if (adbDevice?.DeviceData is null)
        {
            logger.Warn($"No online ADB device found for {deviceId}");
            return false;
        }

        if (!File.Exists(apkPath))
        {
            logger.Warn($"APK file not found: {apkPath}");
            return false;
        }

        try
        {
            await Task.Run(() =>
            {
                using var stream = File.OpenRead(apkPath);
                adbClient.Install(adbDevice.DeviceData, stream);
            });
            logger.Info($"Installed app from {apkPath}");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to install APK: {ex.Message}", ex);
            return false;
        }
    }

    private AdbDevice? GetOnlineAdbDevice(string deviceId)
    {
        var pairedDevice = deviceManager.PairedDevices.FirstOrDefault(d => d.Id == deviceId);
        if (pairedDevice is null) return null;

        return AdbDevices.FirstOrDefault(adbDevice => pairedDevice.IsMatchingAdbDevice(adbDevice));
    }

    /// <summary>
    /// Tries to connect to the device over TCP/IP wirelessly. If connection fails and USB is plugged in, enables TCP/IP mode and retries.
    /// </summary>
    /// <param name="host">The host to connect to</param>
    /// <param name="model">The model of the device to connect to</param>
    public async Task<bool> TryConnectTcp(string host, string model)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        int port = 5555;
        var cleanHost = host.Trim();
        if (cleanHost.Contains(':'))
        {
            var parts = cleanHost.Split(':');
            cleanHost = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedPort))
            {
                port = parsedPort;
            }
        }

        try
        {
            logger.Info($"Trying to connect to adb device: {cleanHost}:{port}");
            var result = await ConnectWireless(cleanHost, port);
            if (result)
            {
                logger.Info($"Connected to adb device: {cleanHost}:{port}");
                return true;
            }

            var cleanTargetModel = model?.Replace('_', ' ').Trim();
            var usbDevice = AdbDevices.FirstOrDefault(d => d.Type is DeviceType.USB && d.IsOnline &&
                (!string.IsNullOrEmpty(cleanTargetModel) &&
                 (!string.IsNullOrEmpty(d.Model) &&
                  (d.Model.Replace('_', ' ').Trim().Equals(cleanTargetModel, StringComparison.OrdinalIgnoreCase) ||
                   d.Model.Replace('_', ' ').Trim().Contains(cleanTargetModel, StringComparison.OrdinalIgnoreCase) ||
                   cleanTargetModel.Contains(d.Model.Replace('_', ' ').Trim(), StringComparison.OrdinalIgnoreCase)))));

            if (usbDevice is null)
            {
                var onlineUsb = AdbDevices.Where(d => d.Type is DeviceType.USB && d.IsOnline).ToList();
                if (onlineUsb.Count == 1)
                {
                    usbDevice = onlineUsb[0];
                }
            }

            if (usbDevice is null) return false;
            
            // If connection failed, try to enable TCP/IP mode using ADB if USB is connected
            var tcpipEnabled = await EnableTcpipMode(usbDevice.Serial);
            if (!tcpipEnabled)
            {
                logger.Error("Failed to enable TCP/IP mode");
                return false;
            }

            // Retry wireless connection with backoff to allow adbd time to restart on port 5555
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                await Task.Delay(500 * attempt);
                result = await ConnectWireless(cleanHost, port);
                if (result)
                {
                    logger.Info($"Successfully connected to {cleanHost}:{port} after enabling TCP/IP mode (attempt {attempt})");
                    return true;
                }
            }

            logger.Error($"TCP/IP connection still failed after enabling TCP/IP mode for {cleanHost}:{port}");
            return false;
        }
        catch (Exception ex)
        {
            logger.Error($"Error in TryConnectTcp for {cleanHost}:{port}: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Enables TCP/IP mode on a device by running "adb -s <serial> tcpip 5555"
    /// </summary>
    private async Task<bool> EnableTcpipMode(string serialId)
    {
        try
        {
            string adbPath = userSettingsService.GeneralSettingsService.AdbPath;
            if (string.IsNullOrEmpty(adbPath))
            {
                logger.Error("ADB path not configured");
                return false;
            }

            logger.Info($"Enabling TCP/IP mode using ADB at: {adbPath}");
            
            // Runs "adb -s <serial> tcpip 5555" to enable TCP/IP mode on the specified device
            var processInfo = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = $"-s {serialId} tcpip 5555",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process is null)
            {
                logger.Error("Failed to start ADB process");
                return false;
            }

            await process.WaitForExitAsync();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            
            if (!string.IsNullOrEmpty(error))
            {
                logger.Warn($"ADB tcpip command error: {error}");
            }

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to enable TCP/IP mode: {ex.Message}", ex);
            return false;
        }
    }

    private async Task AutoSetupWirelessAdbAsync(AdbDevice usbDevice)
    {
        try
        {
            if (usbDevice?.DeviceData is null || usbDevice.State is not DeviceState.Online) return;

            var adbPath = userSettingsService.GeneralSettingsService.AdbPath;
            if (string.IsNullOrEmpty(adbPath)) return;

            // Find device IP: first check paired devices
            string targetIp = string.Empty;
            var pairedDevice = deviceManager.PairedDevices.FirstOrDefault(pd => pd.IsMatchingAdbDevice(usbDevice));
            if (pairedDevice is not null)
            {
                if (!pairedDevice.DeviceSettings.AdbAutoConnect)
                {
                    logger.Debug($"AdbAutoConnect disabled for {usbDevice.Serial}, skipping auto wireless setup");
                    return;
                }

                if (!string.IsNullOrEmpty(pairedDevice.Address))
                {
                    targetIp = pairedDevice.Address;
                }
            }

            // If not found from paired device, query IP directly from Android via adb shell
            if (string.IsNullOrEmpty(targetIp))
            {
                targetIp = await GetDeviceIpAddressAsync(usbDevice.DeviceData);
            }

            if (string.IsNullOrEmpty(targetIp))
            {
                logger.Debug($"Could not determine IP address for USB device {usbDevice.Serial}");
                return;
            }

            // Check if already connected over Wi-Fi
            if (AdbDevices.Any(d => d.Type is DeviceType.WIFI && d.IsOnline && d.Serial.StartsWith(targetIp)))
            {
                logger.Debug($"Wireless ADB already connected for {targetIp}");
                return;
            }

            logger.Info($"Auto-configuring wireless ADB for USB device {usbDevice.Serial} at {targetIp}");
            await TryConnectTcp(targetIp, usbDevice.Model);
        }
        catch (Exception ex)
        {
            logger.Warn($"AutoSetupWirelessAdbAsync encountered an issue: {ex.Message}");
        }
    }

    private async Task<string> GetDeviceIpAddressAsync(DeviceData deviceData)
    {
        try
        {
            var receiver = new ConsoleOutputReceiver();
            await adbClient.ExecuteShellCommandAsync(deviceData, "ip route", receiver);
            var output = receiver.ToString();
            var match = Regex.Match(output, @"\bsrc\s+([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)");
            if (match.Success && !match.Groups[1].Value.StartsWith("127."))
            {
                return match.Groups[1].Value;
            }

            receiver = new ConsoleOutputReceiver();
            await adbClient.ExecuteShellCommandAsync(deviceData, "ip -o -4 addr show", receiver);
            output = receiver.ToString();
            var ipMatches = Regex.Matches(output, @"inet\s+([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)");
            foreach (Match m in ipMatches)
            {
                var ip = m.Groups[1].Value;
                if (!ip.StartsWith("127.") && !ip.StartsWith("169.254."))
                {
                    return ip;
                }
            }
        }
        catch (Exception ex)
        {
            logger.Debug($"Could not query IP address from device shell: {ex.Message}");
        }
        return string.Empty;
    }

    /// <summary>
    /// Restarts the ADB client to pick up TCP/IP mode changes
    /// </summary>
    private async Task RestartAdbClient()
    {
        try
        {
            logger.Info("Restarting ADB client");
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
            logger.Info("ADB client restarted successfully");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to restart ADB client: {ex.Message}", ex);
        }
    }

    public Task DisconnectDeviceAsync(AdbDevice device)
    {
        if (device.Type is not DeviceType.WIFI || string.IsNullOrEmpty(device.Serial))
            return Task.CompletedTask;

        try
        {
            var result = adbClient.Disconnect(device.Serial);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to disconnect ADB device {device.Serial}: {ex.Message}", ex);
        }

        return Task.CompletedTask;
    }

    private async Task GrantSensitiveNotificationAsync(AdbDevice device)
    {
        if (device.DeviceData is null || device.State is not DeviceState.Online) return;

        try
        {
            var receiver = new ConsoleOutputReceiver();
            await adbClient.ExecuteShellCommandAsync(
                device.DeviceData,
                $"appops get {SefirahAndroidPackageId} RECEIVE_SENSITIVE_NOTIFICATIONS",
                receiver);

            if (receiver.ToString().Contains("allow", StringComparison.OrdinalIgnoreCase))
                return;

            logger.Info($"Granting RECEIVE_SENSITIVE_NOTIFICATIONS on {device.Serial}");
            await adbClient.ExecuteShellCommandAsync(
                device.DeviceData,
                $"appops set {SefirahAndroidPackageId} RECEIVE_SENSITIVE_NOTIFICATIONS allow");
        }
        catch (Exception ex)
        {
            logger.Warn($"Could not grant RECEIVE_SENSITIVE_NOTIFICATIONS on {device.Serial}: {ex.Message}", ex);
        }
    }

    /// <summary>Starts the worker via adb shell if it isn't already running.</summary>
    public async Task TryStartWorkerAsync(PairedDevice device, string command)
    {
        if (device.ConnectedAdbDevices.FirstOrDefault() is not { } adbDevice)
            return;

        if (adbDevice.DeviceData is null || adbDevice.State is not DeviceState.Online) return;

        try
        {
            if (!string.IsNullOrWhiteSpace(await ShellAsync(adbDevice, $"pidof {WorkerNiceName}")))
            {
                logger.Debug($"Worker already running on {adbDevice.Serial}");
                return;
            }

            logger.Info($"Starting worker on {adbDevice.Serial}");
            await ShellAsync(adbDevice, command);
            await Task.Delay(1000);

            var pid = (await ShellAsync(adbDevice, $"pidof {WorkerNiceName}")).Trim();
            if (!string.IsNullOrWhiteSpace(pid))
            {
                logger.Info($"Worker running on {adbDevice.Serial} (pid={pid})");
            }
            else
            {
                logger.Warn($"Worker failed to stay up on {adbDevice.Serial}");
            }
        }
        catch (Exception ex)
        {
            logger.Warn($"Could not start worker on {adbDevice.Serial}: {ex.Message}", ex);
        }
    }

    private async Task<string> ShellAsync(AdbDevice device, string command)
    {
        var receiver = new ConsoleOutputReceiver();
        await adbClient.ExecuteShellCommandAsync(device.DeviceData!, command, receiver);
        return receiver.ToString();
    }
}
