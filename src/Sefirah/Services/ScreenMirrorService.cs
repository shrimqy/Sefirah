using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Utils;
using Sefirah.Views.Settings;
using Sefirah.Views.WindowViews;
using Windows.ApplicationModel.DataTransfer;

#if WINDOWS
using Sefirah.Platforms.Windows.HostedApp;
#endif

namespace Sefirah.Services;
public class ScreenMirrorService(
    ILogger logger,
    IUserSettingsService userSettingsService,
    IAdbService adbService,
    IDeviceManager deviceManager,
    RemoteAppRepository remoteAppRepository
) : IScreenMirrorService
{
    private readonly ObservableCollection<AdbDevice> devices = adbService.AdbDevices;

    private readonly Dictionary<string, Process> scrcpyProcesses = [];
    private CancellationTokenSource? cts;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = App.MainWindow.DispatcherQueue;
    
    // Password cache: deviceId -> (password, cachedTime, timeoutMinutes)
    private readonly Dictionary<string, (string Password, DateTime CachedAt, int TimeoutMinutes)> passwordCache = [];

    public async void LaunchAppByPackage(string package)
    {
        var device = deviceManager.ActiveDevice;
        if (device is null) return;

        var app = remoteAppRepository.GetApplicationForDevice(device.Id, package);
        if (app is null)
        {
            logger.Warn($"No application found for package {package} on device {device.Id}");
            return;
        }

        await StartScrcpy(device, app);
    }

    public async Task<bool> StartScrcpy(PairedDevice device, ApplicationItem? app = null)
    {
        Process? process = null;
        CancellationTokenSource? processCts = null;

        var deviceSettings = device.DeviceSettings;
        try
        {
            var scrcpyPath = userSettingsService.GeneralSettingsService.ScrcpyPath;
            if (!File.Exists(scrcpyPath))
            {
                logger.Error($"Scrcpy not found at {scrcpyPath}");
                var result = await dispatcher.EnqueueAsync(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = App.MainWindow.Content!.XamlRoot,
                        Title = "ScrcpyNotFound".GetLocalizedResource(),
                        Content = "ScrcpyNotFoundDescription".GetLocalizedResource(),
                        PrimaryButtonText = "SelectLocation".GetLocalizedResource(),
                        DefaultButton = ContentDialogButton.Primary,
                        CloseButtonText = "Dismiss".GetLocalizedResource()
                    };

                    var dialogResult = await dialog.ShowAsync();
                    if (dialogResult is ContentDialogResult.Primary)
                    {
                        scrcpyPath = await SelectScrcpyLocationClick();
                        return !string.IsNullOrEmpty(scrcpyPath) && File.Exists(scrcpyPath);
                    }
                    return false;
                });

                if (!result) return false;
            }

            List<string> argBuilder = [];

            var isStartApp = app is not null;
            if (isStartApp)
            {
                IconUtils.SetScrcpyWindowIcon(device.Id, app!.PackageName);
                argBuilder.Add($"--start-app={app.PackageName}");
                if (!string.IsNullOrEmpty(app.AppName))
                    argBuilder.Add($"--window-title=\"{app.AppName}\"");
            }

            var preDefinedArgs = deviceSettings.CustomArguments;
            string? selectedDeviceSerial = null;

            // if preDefinedArgs contains -s, --serial, or --tcpip
            if (!string.IsNullOrEmpty(preDefinedArgs))
            {
                // Check if preDefinedArgs contains any of the flags that specify a device serial
                bool hasDeviceSerialFlag = Regex.IsMatch(preDefinedArgs, @"(?:^|\s)(?:-s|--serial|--tcpip)");
                
                if (hasDeviceSerialFlag)
                {
                    // Extract serial from "-s VALUE" format
                    var shortSerialPattern = @"(?:^|\s)-s\s+(\S+)";
                    var shortMatch = Regex.Match(preDefinedArgs, shortSerialPattern);
                    if (shortMatch.Success)
                    {
                        selectedDeviceSerial = shortMatch.Groups[1].Value;
                        preDefinedArgs = Regex.Replace(preDefinedArgs, shortSerialPattern, "").Trim();
                    }
                    
                    // Extract serial from "--serial=VALUE" format
                    var longSerialPattern = @"(?:^|\s)--serial=(\S+)";
                    var longMatch = Regex.Match(preDefinedArgs, longSerialPattern);
                    if (longMatch.Success)
                    {
                        selectedDeviceSerial = longMatch.Groups[1].Value;
                        preDefinedArgs = Regex.Replace(preDefinedArgs, longSerialPattern, "").Trim();
                    }
                    
                    // Extract value from "--tcpip=VALUE" format
                    var tcpipPattern = @"(?:^|\s)--tcpip=(\S+)";
                    var tcpipMatch = Regex.Match(preDefinedArgs, tcpipPattern);
                    if (tcpipMatch.Success)
                    {
                        selectedDeviceSerial = tcpipMatch.Groups[1].Value;
                        preDefinedArgs = Regex.Replace(preDefinedArgs, tcpipPattern, "").Trim();
                    }
                }
                
                if (!string.IsNullOrWhiteSpace(preDefinedArgs))
                {
                    argBuilder.Add(preDefinedArgs);
                }
            }

            if (string.IsNullOrEmpty(selectedDeviceSerial))
            {
                selectedDeviceSerial = await DeviceSelection(deviceSettings, argBuilder, device);
            }

            if (string.IsNullOrEmpty(selectedDeviceSerial)) return false;

            // Virtual-display app launches don't need the physical screen unlocked.
            // Await password prompt if needed; the ADB unlock sequence itself is fire-and-forget.
            if (!(isStartApp && deviceSettings.IsVirtualDisplayEnabled))
                await TryUnlockDevice(device, deviceSettings, selectedDeviceSerial);
            
            argBuilder.Add($"-s {selectedDeviceSerial}");

            // General deviceSettings
            if (deviceSettings.ScreenOff)
            {
                argBuilder.Add("--turn-screen-off");
            }

            if (deviceSettings.PhysicalKeyboard)
            {
                argBuilder.Add("--keyboard=uhid");
            }

            if (!deviceSettings.ScrcpyClipboardAutosync)
            {
                argBuilder.Add("--no-clipboard-autosync");
            }

            // Video deviceSettings
            if (deviceSettings.DisableVideoForwarding)
            {
                argBuilder.Add("--no-video");
            }

            if (deviceSettings.VideoCodec != 0)
            {
                var videoOptions = adbService.GetVideoCodecOptions(device.Model);
                if (deviceSettings.VideoCodec < videoOptions.Count)
                {
                    argBuilder.Add($"{videoOptions[deviceSettings.VideoCodec].Command}");
                }
            }

            if (!string.IsNullOrEmpty(deviceSettings.VideoResolution))
            {
                argBuilder.Add($"--max-size={deviceSettings.VideoResolution}");
            }

            var videoBitrate = isStartApp && deviceSettings.FlexDisplay ? "16M" : deviceSettings.VideoBitrate;
            if (!string.IsNullOrEmpty(videoBitrate))
            {
                argBuilder.Add($"--video-bit-rate={videoBitrate}");
            }

            if (deviceSettings.VideoBuffer > 0)
            {
                argBuilder.Add($"--video-buffer={deviceSettings.VideoBuffer}");
            }

            if (deviceSettings.FrameRate > 0)
            {
                argBuilder.Add($"--max-fps={deviceSettings.FrameRate}");
            }

            if (!string.IsNullOrEmpty(deviceSettings.Crop))
            {
                argBuilder.Add($"--crop={deviceSettings.Crop}");
            }

            if (deviceSettings.DisplayOrientation != 0)
            {
                argBuilder.Add($"--orientation={adbService.DisplayOrientationOptions[deviceSettings.DisplayOrientation].Command}");
            }

            if (!string.IsNullOrEmpty(deviceSettings.Display))
            {
                argBuilder.Add($"--display-id={deviceSettings.Display}");
            }

            // Audio deviceSettings
            if (!string.IsNullOrEmpty(deviceSettings.AudioBitrate))
            {
                argBuilder.Add($"--audio-bit-rate={deviceSettings.AudioBitrate}");
            }

            if (deviceSettings.AudioBuffer > 0)
            {
                argBuilder.Add($"--audio-buffer={deviceSettings.AudioBuffer}");
            }

            if (deviceSettings.AudioOutputBuffer > 0)
            {
                argBuilder.Add($"--audio-output-buffer={deviceSettings.AudioOutputBuffer}");
            }

            if (deviceSettings.ForwardMicrophone)
            {
                argBuilder.Add("--audio-source=mic");
            }

            switch (deviceSettings.AudioOutputMode)
            {
                case AudioOutputModeType.Remote:
                    argBuilder.Add("--no-audio");
                    break;
                case AudioOutputModeType.Both:
                    argBuilder.Add("--audio-dup");
                    break;
            }

            if (deviceSettings.AudioCodec != 0)
            {
                var audioOptions = adbService.GetAudioCodecOptions(device.Model);
                if (deviceSettings.AudioCodec < audioOptions.Count)
                {
                    argBuilder.Add($"{audioOptions[deviceSettings.AudioCodec].Command}");
                }
            }

            if (isStartApp)
            {
                if (!string.IsNullOrEmpty(deviceSettings.VirtualDisplaySize) && deviceSettings.IsVirtualDisplayEnabled)
                {
                    argBuilder.Add($"--new-display={deviceSettings.VirtualDisplaySize}");
                }
                else if (deviceSettings.IsVirtualDisplayEnabled)
                {
                    argBuilder.Add("--new-display");
                }
                else if (scrcpyProcesses.Count > 0)
                {
                    // Check for existing processes for this device and terminate them
                    // when virtual display is not enabled
                    if (scrcpyProcesses.TryGetValue(selectedDeviceSerial, out var existingProcess))
                    {
                        try
                        {
                            if (!existingProcess.HasExited)
                            {
                                existingProcess.Kill();
                            }
                            scrcpyProcesses.Remove(selectedDeviceSerial);
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Failed to terminate existing process: {ex.Message}", ex);
                        }
                    }
                }

                if (deviceSettings.FlexDisplay && deviceSettings.IsVirtualDisplayEnabled)
                {
                    argBuilder.Add("-x");
                    argBuilder.Add("--keep-active");
                }
            }

            cts?.Cancel();
            cts?.Dispose();
            processCts = new CancellationTokenSource();
            cts = processCts;
            
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = scrcpyPath,
                    Arguments = string.Join(" ", argBuilder),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            if (isStartApp && File.Exists(IconUtils.ScrcpyWindowIconPath))
            {
                process.StartInfo.EnvironmentVariables["SCRCPY_ICON_DIR"] = IconUtils.ScrcpyIconsDirectory;
                process.StartInfo.EnvironmentVariables["SCRCPY_ICON_PATH"] = IconUtils.ScrcpyWindowIconPath;
            }

            bool started;
            try
            {
                started = process.Start();
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to start scrcpy: {ex.Message}", ex);
                process?.Dispose();
                processCts.Dispose();
                if (ReferenceEquals(cts, processCts)) cts = null;
                return false;
            }

            if (!started)
            {
                logger.Error("Failed to start scrcpy process");
                process?.Dispose();
                processCts.Dispose();
                if (ReferenceEquals(cts, processCts)) cts = null;
                return false;
            }

            StartProcessMonitoring(process, processCts, selectedDeviceSerial);

#if WINDOWS
            if (isStartApp)
            {
                var appUserModelId = HostedPackageIdentity.GetAppUserModelId(app!.PackageName);
                if (!string.IsNullOrEmpty(appUserModelId))
                    _ = WindowShellBinder.TryBindAsync(process, appUserModelId, processCts.Token);
            }
#endif

            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Error in StartScrcpy", ex);
            processCts?.Dispose();
            if (ReferenceEquals(cts, processCts)) cts = null;
            process?.Dispose();
            return false;
        }
    }

    private async Task<string?> DeviceSelection(IDeviceSettingsService deviceSettings, List<string> argBuilder, PairedDevice device)
    {
        string? selectedDeviceSerial = null;
        var devicePreferenceType = deviceSettings.ScrcpyDevicePreference;

        var pairedDevices = devices.Where(device.IsMatchingAdbDevice).ToList();
        if (pairedDevices.Count > 0)
        {
            switch (devicePreferenceType)
            {
                case ScrcpyDevicePreferenceType.Usb:
                    selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.USB)?.Serial;
                    break;
                case ScrcpyDevicePreferenceType.Tcpip:
                    selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.WIFI)?.Serial;
                    if (string.IsNullOrEmpty(selectedDeviceSerial) && !string.IsNullOrEmpty(device.Address))
                    {
                        if (await adbService.TryConnectTcp(device.Address, device.Model))
                        {
                            selectedDeviceSerial = device.Address.Contains(':') ? device.Address : $"{device.Address}:5555";
                        }
                    }
                    break;
                case ScrcpyDevicePreferenceType.Auto:
                    // Prioritize USB if connected, otherwise use Wi-Fi
                    var usbDevice = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.USB);
                    if (usbDevice is not null)
                    {
                        selectedDeviceSerial = usbDevice.Serial;
                    }
                    else
                    {
                        var wifiDev = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.WIFI);
                        if (wifiDev is not null)
                        {
                            selectedDeviceSerial = wifiDev.Serial;
                        }
                        else if (!string.IsNullOrEmpty(device.Address))
                        {
                            if (await adbService.TryConnectTcp(device.Address, device.Model))
                            {
                                selectedDeviceSerial = device.Address.Contains(':') ? device.Address : $"{device.Address}:5555";
                            }
                        }
                    }
                    break;
                case ScrcpyDevicePreferenceType.AskEverytime:
                    selectedDeviceSerial = await ShowDeviceSelectionDialog(pairedDevices);
                    if (string.IsNullOrEmpty(selectedDeviceSerial))
                    {
                        logger.Warn("No device selected for scrcpy");
                        return null;
                    }
                    break;
            }
        }
        else if (!string.IsNullOrEmpty(device.Address))
        {
            // No ADB devices matched currently, try to connect via TCP over Wi-Fi
            if (await adbService.TryConnectTcp(device.Address, device.Model))
            {
                selectedDeviceSerial = device.Address.Contains(':') ? device.Address : $"{device.Address}:5555";
            }
        }

        if (string.IsNullOrEmpty(selectedDeviceSerial) && devices.Any(d => d.IsOnline && !string.IsNullOrEmpty(d.Serial)))
        {
            // If no paired devices found, show dialog to select from online devices
            selectedDeviceSerial = await ShowDeviceSelectionDialog(devices.Where(d => d.IsOnline).ToList());
        }
        else if (string.IsNullOrEmpty(selectedDeviceSerial))
        {
            logger.Warn("No online devices found from adb");
            await dispatcher.EnqueueAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = App.MainWindow.Content!.XamlRoot,
                    Title = "AdbDeviceOffline".GetLocalizedResource(),
                    Content = "AdbDeviceOfflineDescription".GetLocalizedResource(),
                    CloseButtonText = "Dismiss".GetLocalizedResource()
                };
                await dialog.ShowAsync();
            });
            return null;
        }

        return selectedDeviceSerial;
    }

    private async Task TryUnlockDevice(PairedDevice device, IDeviceSettingsService deviceSettings, string selectedDeviceSerial)
    {
        if (!deviceSettings.UnlockDeviceBeforeLaunch)
            return;

        var commands = deviceSettings.UnlockCommands
            .Where(c => !string.IsNullOrWhiteSpace(c.Command))
            .ToList();
        if (commands.Count == 0)
            return;

        var adbDevice = devices.FirstOrDefault(d => d.Serial == selectedDeviceSerial);
        if (adbDevice?.DeviceData is null)
        {
            logger.Warn($"Cannot unlock: no ADB device found for serial {selectedDeviceSerial}");
            return;
        }

        if (!await adbService.IsLocked(adbDevice.DeviceData))
            return;

        if (commands.Any(c => c.Command.Contains("%pwd%")))
        {
            var timeoutSeconds = deviceSettings.UnlockTimeout;
            string? password = timeoutSeconds > 0 ? GetCachedPassword(device.Id, timeoutSeconds) : null;

            if (password is null)
            {
                password = await dispatcher.EnqueueAsync(PasswordInputWindow.ShowAsync);
                if (password is null)
                    return;

                if (timeoutSeconds > 0)
                    CachePassword(device.Id, password, timeoutSeconds);
            }

            // Copy so we don't persist the resolved password into settings
            commands = [.. commands.Select(c => new UnlockCommandEntry
            {
                Command = c.Command.Replace("%pwd%", password),
                DelayMs = c.DelayMs
            })];
        }

        // Unlock commands run in the background; scrcpy can start immediately after.
        adbService.UnlockDevice(adbDevice.DeviceData, commands);
    }

    private void StartProcessMonitoring(Process process, CancellationTokenSource processCts, string deviceSerial)
    {
        var errorOutput = new StringBuilder();
        
        process.OutputDataReceived += (_, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data))
                logger.Info($"scrcpy: {e.Data}");
        };
        
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                logger.Error($"scrcpy error: {e.Data}");
                lock (errorOutput)
                {
                    errorOutput.AppendLine(e.Data);
                }
            }
        };
        
        process.Exited += (_, _) => logger.Info("scrcpy process terminated");
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        logger.Info($"scrcpy process started {process.Id}");
       

        scrcpyProcesses.Add(deviceSerial, process);

        Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync(processCts.Token);
                logger.Info($"scrcpy process exited with code: {process.ExitCode}");
                
                if (process.ExitCode != 0 && process.ExitCode != 2)
                {
                    string errorMessage;
                    lock (errorOutput)
                    {
                        errorMessage = $"Scrcpy process exited with code {process.ExitCode}\n\nError Output:\n{errorOutput.ToString().TrimEnd()}";
                    }
                    logger.Error($"Scrcpy failed: {errorMessage}");

                    await dispatcher.EnqueueAsync(async () =>
                    {
                        var scrollViewer = new ScrollViewer
                        {
                            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            MaxHeight = 300,
                            Content = new TextBlock
                            {
                                Text = errorMessage,
                                IsTextSelectionEnabled = true,
                                TextWrapping = TextWrapping.Wrap
                            }
                        };
                        
                        var errorDialog = new ContentDialog
                        {
                            XamlRoot = App.MainWindow.Content!.XamlRoot,
                            Title = "ScrcpyErrorTitle".GetLocalizedResource(),
                            Content = scrollViewer,
                            CloseButtonText = "Dismiss".GetLocalizedResource(),
                            SecondaryButtonText = "CopyError".GetLocalizedResource()
                        };
                        
                        var result = await errorDialog.ShowAsync();
                        if (result is ContentDialogResult.Secondary)
                        {
                            var dataPackage = new DataPackage();
                            dataPackage.SetText(errorMessage);
                            Clipboard.SetContent(dataPackage);
                            logger.Info("Scrcpy error output copied to clipboard");
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.Error("Error monitoring scrcpy process", ex);
            }
            finally
            {
                process.Dispose();
                scrcpyProcesses.Remove(deviceSerial);

                processCts.Dispose();
                if (ReferenceEquals(cts, processCts))
                {
                    cts = null;
                }
            }
        }, processCts.Token);
    }

    private async Task<string?> ShowDeviceSelectionDialog(List<AdbDevice> onlineDevices)
    {
        string? selectedDeviceSerial = null;
        
        await dispatcher.EnqueueAsync(async () =>
        {
            var deviceOptions = new List<ComboBoxItem>();
            foreach (var device in onlineDevices)
            {
                var displayName = device.Model ?? "Unknown";
                var item = new ComboBoxItem
                {
                    Content = $"{displayName} - {device.Type} ({device.Serial})",
                    Tag = device.Serial
                };
                deviceOptions.Add(item);
            }

            var deviceSelector = new ComboBox
            {
                ItemsSource = deviceOptions,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = 0
            };

            var dialog = new ContentDialog
            {
                XamlRoot = App.MainWindow.Content!.XamlRoot,
                Title = "SelectDevice".GetLocalizedResource(),
                Content = deviceSelector,
                PrimaryButtonText = "Start".GetLocalizedResource(),
                CloseButtonText = "Cancel".GetLocalizedResource(),
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result is ContentDialogResult.Primary && deviceSelector.SelectedItem is ComboBoxItem selected)
            {
                selectedDeviceSerial = selected.Tag as string;
            }
        });

        return selectedDeviceSerial;
    }

    public async Task<string> SelectScrcpyLocationClick()
    {
        var file = await PickerHelper.PickFileAsync();
        if (file?.Path is string path)
        {
            userSettingsService.GeneralSettingsService.ScrcpyPath = path;
            GeneralPage.TrySetCompanionTool(path, "adb.exe", p => userSettingsService.GeneralSettingsService.AdbPath = p);
            await adbService.StartAsync();
            return path;
        }
        return string.Empty;
    }

    private string? GetCachedPassword(string deviceId, int currentTimeout)
    {
        if (passwordCache.TryGetValue(deviceId, out var cacheEntry))
        {
            var (password, cachedAt, cachedTimeout) = cacheEntry;

            if (currentTimeout == cachedTimeout && DateTime.Now <= cachedAt.AddMinutes(cachedTimeout))
            {
                return password;
            }
            passwordCache.Remove(deviceId);
        }
        
        return null;
    }

    private void CachePassword(string deviceId, string password, int timeoutMinutes)
    {
        passwordCache[deviceId] = (password, DateTime.Now, timeoutMinutes);
    }
}
