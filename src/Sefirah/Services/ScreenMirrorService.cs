using System.Text;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Dialogs;
using Sefirah.Extensions;
using Sefirah.Utils;
using Sefirah.Views.Settings;
using Uno.Logging;
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
    private readonly Microsoft.UI.Dispatching.DispatcherQueue? dispatcher = App.MainWindow?.DispatcherQueue;
    
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
                var result = await dispatcher!.EnqueueAsync(async () =>
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
                    if (dialogResult == ContentDialogResult.Primary)
                    {
                        scrcpyPath = await SelectScrcpyLocationClick();
                        return !string.IsNullOrEmpty(scrcpyPath) && File.Exists(scrcpyPath);
                    }
                    return false;
                });

                if (!result) return false;
            }

            var devicePreferenceType = deviceSettings.ScrcpyDevicePreference;
            string? selectedDeviceSerial = null;

            List<string> argBuilder = [];
            if (!string.IsNullOrEmpty(customArgs))
            {
                argBuilder.Add(customArgs);
            }

            var pairedDevices = devices.Where(d => d != null && d.Model == device.Model).ToList();
            if (pairedDevices.Count > 0)
            {
                switch (devicePreferenceType)
                {
                    case ScrcpyDevicePreferenceType.Usb:
                        selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type == DeviceType.USB)?.Serial;
                        break;
                    case ScrcpyDevicePreferenceType.Tcpip:
                        selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type == DeviceType.WIFI)?.Serial;
                        break;
                    case ScrcpyDevicePreferenceType.Auto:
                        if (pairedDevices.FirstOrDefault(d => d.Type == DeviceType.USB) != null)
                        {
                            if (deviceSettings.AdbTcpipModeEnabled) 
                            {
                                argBuilder.Add("--tcpip");
                            }
                            selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type == DeviceType.USB)?.Serial;
                        }
                        else
                        {
                            selectedDeviceSerial = pairedDevices.FirstOrDefault(d => d.Type == DeviceType.WIFI)?.Serial;
                        }
                        break;
                    case ScrcpyDevicePreferenceType.AskEverytime:
                        selectedDeviceSerial = await ShowDeviceSelectionDialog(pairedDevices);
                        if (string.IsNullOrEmpty(selectedDeviceSerial))
                        {
                            logger.LogWarning("No device selected for scrcpy");
                            return false;
                        }
                        break;
                }
                var commands = deviceSettings.UnlockCommands?.Trim()
                    .Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
                var adbDevice = pairedDevices.FirstOrDefault(d => d.Serial == selectedDeviceSerial);
                if (adbDevice == null || !adbDevice.DeviceData.HasValue) return false;

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
                        if (password == null)
                        {
                            password = await ShowPasswordInputDialog();
                            if (password == null) return false;
                            
                            // Only cache the password if timeout is greater than 0
                            if (timeoutSeconds > 0)
                            {
                                CachePassword(device.Id, password, timeoutSeconds);
                            }
                        }
                        
                        // Replace password placeholders with actual password
                        commands = commands.Select(c => c.Replace("%pwd%", password)).ToList();
                    }
                    
                    if (adbDevice != null && adbDevice.DeviceData.HasValue)
                    {
                        adbService.UnlockDevice(adbDevice.DeviceData.Value, commands);
                    }
                }
            }
            else if(deviceSettings.AdbTcpipModeEnabled && device.Session != null)
            {
                var connectedSessionIpAddress = device.Session.Socket.RemoteEndPoint?.ToString()?.Split(':')[0];
                if (await adbService.ConnectWireless(connectedSessionIpAddress))
                {
                    selectedDeviceSerial = $"{connectedSessionIpAddress}:5555";
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
                dispatcher?.EnqueueAsync(async () =>
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
                return false;
            }

            // Validate that we have a selected device
            if (string.IsNullOrEmpty(selectedDeviceSerial)) return false;
            
            argBuilder.Add($"-s {selectedDeviceSerial}");

            // Build arguments for scrcpy with the selected device
            var (args, deviceSerial) = BuildScrcpyArguments(argBuilder, selectedDeviceSerial!, deviceSettings);
            
            cts?.Cancel();
            cts?.Dispose();
            processCts = new CancellationTokenSource();
            cts = processCts;
            
            process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = scrcpyPath,
                    Arguments = args,
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
                logger.Info($"Using custom scrcpy icon: {iconPath}");
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

            StartProcessMonitoring(process, processCts, deviceSerial);
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

                    await dispatcher!.EnqueueAsync(async () =>
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
                        if (result == ContentDialogResult.Secondary)
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
        
        await dispatcher!.EnqueueAsync(async () =>
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

            if (result == ContentDialogResult.Primary && deviceSelector.SelectedItem is ComboBoxItem selected)
            {
                selectedDeviceSerial = selected.Tag as string;
            }
        });

        return selectedDeviceSerial;
    }

    private (string, string) BuildScrcpyArguments(List<string> args, string deviceSerial, IDeviceSettingsService settings)
    {

        var preDefinedArgs = settings.CustomArguments;

        if (!string.IsNullOrEmpty(preDefinedArgs))
        {
            args.Add(preDefinedArgs);
        }
        
        // General settings
        if (settings.ScreenOff)
        {
            args.Add("--turn-screen-off");
        }
        
        if (settings.PhysicalKeyboard)
        {
            args.Add("--keyboard=uhid");
        }
        
        // Video settings
        if (settings.DisableVideoForwarding)
        {
            args.Add("--no-video");
        }

        if (settings.VideoCodec != 0)
        {
            args.Add($"{adbService.VideoCodecOptions[settings.VideoCodec].Command}");
        }   
        
        if (!string.IsNullOrEmpty(settings.VideoResolution))
        {
            args.Add($"--max-size={settings.VideoResolution}");
        }
        
        if (!string.IsNullOrEmpty(settings.VideoBitrate))
        {
            args.Add($"--video-bit-rate={settings.VideoBitrate}");
        }
        
        if (!string.IsNullOrEmpty(settings.VideoBuffer))
        {
            args.Add($"--video-buffer={settings.VideoBuffer}");
        }
        
        if (!string.IsNullOrEmpty(settings.FrameRate))
        {
            args.Add($"--max-fps={settings.FrameRate}");
        }
        
        if (!string.IsNullOrEmpty(settings.Crop))
        {
            args.Add($"--crop={settings.Crop}");
        }

        if (settings.DisplayOrientation != 0)
        {
            args.Add($"--orientation={adbService.DisplayOrientationOptions[settings.DisplayOrientation].Command}");
        }
        
        if (!string.IsNullOrEmpty(settings.Display))
        {
            args.Add($"--display-id={settings.Display}");
        }

        // Audio settings
        if (!string.IsNullOrEmpty(settings.AudioBitrate))
        {
            args.Add($"--audio-bit-rate={settings.AudioBitrate}");
        }

        if (!string.IsNullOrEmpty(settings.AudioBuffer))
        {
            args.Add($"--audio-buffer={settings.AudioBuffer}");
        }

        if (!string.IsNullOrEmpty(settings.AudioOutputBuffer))
        {
            args.Add($"--audio-output-buffer={settings.AudioOutputBuffer}");
        }
        
        if (settings.ForwardMicrophone)
        {
            args.Add("--audio-source=mic");
        }

        switch (settings.AudioOutputMode)
        {
            case AudioOutputModeType.Remote:
                args.Add("--no-audio");
                break;
            case AudioOutputModeType.Both:
                args.Add("--audio-dup");
                break;
        }

        if (settings.AudioCodec != 0)
        {
            args.Add($"{adbService.AudioCodecOptions[settings.AudioCodec].Command}");
        }

        if (args[0].StartsWith ("--start-app"))
        {
            if (!string.IsNullOrEmpty(settings.VirtualDisplaySize) && settings.IsVirtualDisplayEnabled)
            {
                args.Add($"--new-display={settings.VirtualDisplaySize}");
            }
            else if (settings.IsVirtualDisplayEnabled)
            {
                args.Add("--new-display");
            }
            else if (scrcpyProcesses.Count > 0)
            {
                // Check for existing processes for this device and terminate them
                // when virtual display is not enabled
                if (scrcpyProcesses.TryGetValue(deviceSerial, out var existingProcess))
                {
                    try
                    {
                        if (!existingProcess.HasExited)
                        {
                            existingProcess.Kill();
                        }
                        scrcpyProcesses.Remove(deviceSerial);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Failed to terminate existing process: {ex.Message}", ex);
                    }
                }
            }
        }

        return (string.Join(" ", args), deviceSerial);
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
        
        await dispatcher!.EnqueueAsync(async () =>
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
