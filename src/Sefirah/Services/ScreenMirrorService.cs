using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Dialogs;
using Sefirah.Utils;
using Sefirah.Views.Settings;
using Windows.ApplicationModel.DataTransfer;

namespace Sefirah.Services;
public class ScreenMirrorService(
    ILogger<ScreenMirrorService> logger,
    IUserSettingsService userSettingsService,
    IAdbService adbService
) : IScreenMirrorService
{
    private readonly ObservableCollection<AdbDevice> devices = adbService.AdbDevices;

    private Dictionary<string, Process> scrcpyProcesses = [];
    private CancellationTokenSource? cts;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue dispatcher = App.MainWindow.DispatcherQueue;
    
    // Password cache: deviceId -> (password, cachedTime, timeoutMinutes)
    private readonly Dictionary<string, (string Password, DateTime CachedAt, int TimeoutMinutes)> passwordCache = [];
    
    public async Task<bool> StartScrcpy(PairedDevice device, string? customArgs = null, string? iconPath = null)
    {
        Process? process = null;
        CancellationTokenSource? processCts = null;

        var deviceSettings = device.DeviceSettings;
        try
        {
            var scrcpyPath = userSettingsService.GeneralSettingsService.ScrcpyPath;
            if (!File.Exists(scrcpyPath))
            {
                logger.LogError("Scrcpy not found at {ScrcpyPath}", scrcpyPath);
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
            if (!string.IsNullOrEmpty(customArgs))
            {
                argBuilder.Add(customArgs);
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
            
            argBuilder.Add($"-s {selectedDeviceSerial}");

            argBuilder = BuildScrcpyArguments(argBuilder, deviceSettings, device.Model ?? "Unknown");

            if (argBuilder[0].StartsWith("--start-app"))
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
                            logger.LogError($"Failed to terminate existing process: {ex.Message}", ex);
                        }
                    }
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

            if (!string.IsNullOrEmpty(iconPath))
            {
                process.StartInfo.EnvironmentVariables["SCRCPY_ICON_PATH"] = iconPath;
            }

            bool started;
            try
            {
                started = process.Start();
            }
            catch (Exception ex)
            {
                logger.LogError($"Failed to start scrcpy: {ex.Message}", ex);
                process?.Dispose();
                processCts.Dispose();
                if (ReferenceEquals(cts, processCts)) cts = null;
                return false;
            }

            if (!started)
            {
                logger.LogError("Failed to start scrcpy process");
                process?.Dispose();
                processCts.Dispose();
                if (ReferenceEquals(cts, processCts)) cts = null;
                return false;
            }

            StartProcessMonitoring(process, processCts, selectedDeviceSerial);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error in StartScrcpy {ex}", ex);
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

        var pairedDevices = devices.Where(d => d is not null && d.Model == device.Model).ToList();
        if (pairedDevices.Count > 0)
        {
            switch (devicePreferenceType)
            {
                case ScrcpyDevicePreferenceType.Usb:
                    selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.USB)?.Serial;
                    break;
                case ScrcpyDevicePreferenceType.Tcpip:
                    selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.WIFI)?.Serial;
                    break;
                case ScrcpyDevicePreferenceType.Auto:
                    // prioritize USB first to check if the auto tcpip is enabled
                    var usbDevice = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.USB);
                    if (usbDevice is not null)
                    {
                        if (deviceSettings.AdbTcpipModeEnabled)
                        {
                            argBuilder.Add("--tcpip");
                        }
                        selectedDeviceSerial = usbDevice.Serial;
                    }
                    else
                    {
                        selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type is DeviceType.WIFI)?.Serial;
                    }
                    break;
                case ScrcpyDevicePreferenceType.AskEverytime:
                    selectedDeviceSerial = await ShowDeviceSelectionDialog(pairedDevices);
                    if (string.IsNullOrEmpty(selectedDeviceSerial))
                    {
                        logger.LogWarning("No device selected for scrcpy");
                        return null;
                    }
                    break;
            }
            var commands = deviceSettings.UnlockCommands?.Trim()
                .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
            var adbDevice = pairedDevices.FirstOrDefault(d => d.Serial == selectedDeviceSerial);
            if (adbDevice is null || !adbDevice.DeviceData.HasValue) return null;

            if (commands?.Count > 0 && await adbService.IsLocked(adbDevice.DeviceData.Value))
            {
                // Check if any command contains password placeholder
                var hasPasswordPlaceholder = commands.Any(c => c.Contains("%pwd%"));
                string? password = null;

                if (hasPasswordPlaceholder)
                {
                    // Only use password caching if timeout is greater than 0
                    var timeoutSeconds = deviceSettings.UnlockTimeout;
                    if (timeoutSeconds > 0)
                    {
                        // Try to get cached password first
                        password = GetCachedPassword(device.Id, timeoutSeconds);
                    }

                    // If no cached password or caching is disabled, ask user for password
                    if (password is null)
                    {
                        password = await ShowPasswordInputDialog();
                        if (password is null) return null;

                        // Only cache the password if timeout is greater than 0
                        if (timeoutSeconds > 0)
                        {
                            CachePassword(device.Id, password, timeoutSeconds);
                        }
                    }

                    // Replace password placeholders with actual password
                    commands = commands.Select(c => c.Replace("%pwd%", password)).ToList();
                }
                adbService.UnlockDevice(adbDevice.DeviceData.Value, commands);
            }
        }
        else if (deviceSettings.AdbTcpipModeEnabled && device.Session is not null)
        {
            if (await adbService.TryConnectTcp(device.Address, device.Model))
            {
                selectedDeviceSerial = $"{device.Address}:5555";
            }
        }
        else if (devices.Any(d => d.IsOnline && !string.IsNullOrEmpty(d.Serial)))
        {
            // If no paired devices found, show dialog to select from online devices
            selectedDeviceSerial = await ShowDeviceSelectionDialog(devices.Where(d => d.IsOnline).ToList());
        }
        else
        {
            logger.LogWarning("No online devices found from adb");
            dispatcher.EnqueueAsync(async () =>
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

    private List<string> BuildScrcpyArguments(List<string> argBuilder, IDeviceSettingsService deviceSettings, string deviceModel)
    {
        // General deviceSettings
        if (deviceSettings.ScreenOff)
        {
            argBuilder.Add("--turn-screen-off");
        }

        if (deviceSettings.PhysicalKeyboard)
        {
            argBuilder.Add("--keyboard=uhid");
        }

        // Video deviceSettings
        if (deviceSettings.DisableVideoForwarding)
        {
            argBuilder.Add("--no-video");
        }

        if (deviceSettings.VideoCodec != 0)
        {
            var videoOptions = adbService.GetVideoCodecOptions(deviceModel);
            if (deviceSettings.VideoCodec < videoOptions.Count)
            {
                argBuilder.Add($"{videoOptions[deviceSettings.VideoCodec].Command}");
            }
        }

        if (!string.IsNullOrEmpty(deviceSettings.VideoResolution))
        {
            argBuilder.Add($"--max-size={deviceSettings.VideoResolution}");
        }

        if (!string.IsNullOrEmpty(deviceSettings.VideoBitrate))
        {
            argBuilder.Add($"--video-bit-rate={deviceSettings.VideoBitrate}");
        }

        if (!string.IsNullOrEmpty(deviceSettings.VideoBuffer))
        {
            argBuilder.Add($"--video-buffer={deviceSettings.VideoBuffer}");
        }

        if (!string.IsNullOrEmpty(deviceSettings.FrameRate))
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

        if (!string.IsNullOrEmpty(deviceSettings.AudioBuffer))
        {
            argBuilder.Add($"--audio-buffer={deviceSettings.AudioBuffer}");
        }

        if (!string.IsNullOrEmpty(deviceSettings.AudioOutputBuffer))
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
            var audioOptions = adbService.GetAudioCodecOptions(deviceModel);
            if (deviceSettings.AudioCodec < audioOptions.Count)
            {
                argBuilder.Add($"{audioOptions[deviceSettings.AudioCodec].Command}");
            }
        }

        return argBuilder;
    }

    private void StartProcessMonitoring(Process process, CancellationTokenSource processCts, string deviceSerial)
    {
        var errorOutput = new StringBuilder();
        
        process.OutputDataReceived += (_, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data))
                logger.LogInformation($"scrcpy: {e.Data}");
        };
        
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                logger.LogError($"scrcpy error: {e.Data}");
                lock (errorOutput)
                {
                    errorOutput.AppendLine(e.Data);
                }
            }
        };
        
        process.Exited += (_, _) => logger.LogInformation("scrcpy process terminated");
        
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        logger.LogInformation("scrcpy process started {pid}", process.Id);
       

        scrcpyProcesses.Add(deviceSerial, process);

        Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync(processCts.Token);
                logger.LogInformation("scrcpy process exited with code: {exitCode}", process.ExitCode);
                
                if (process.ExitCode != 0 && process.ExitCode != 2)
                {
                    string errorMessage;
                    lock (errorOutput)
                    {
                        errorMessage = $"Scrcpy process exited with code {process.ExitCode}\n\nError Output:\n{errorOutput.ToString().TrimEnd()}";
                    }
                    logger.LogError("Scrcpy failed: {error}", errorMessage);

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
                            logger.LogInformation("Scrcpy error output copied to clipboard");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException)
                {
                    logger.LogError("Error monitoring scrcpy process {ex}", ex);
                }
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

    private async Task<string?> ShowPasswordInputDialog()
    {
        string? password = null;
        
        await dispatcher.EnqueueAsync(async () =>
        {
            var dialog = new PasswordInputDialog
            {
                XamlRoot = App.MainWindow.Content!.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                password = dialog.Password;
            }
        });

        return password;
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
