using AdvancedSharpAdbClient.Models;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.Utils;
using Sefirah.App.ViewModels.Settings;
using System.Text;
using Windows.ApplicationModel.DataTransfer;

namespace Sefirah.App.Services;
public class ScreenMirrorService(
    ILogger logger,
    IUserSettingsService userSettingsService,
    IAdbService adbService,
    ISessionManager sessionManager
) : IScreenMirrorService
{
    private ObservableCollection<AdbDevice> devices = adbService.AdbDevices;
    private List<Process> scrcpyProcesses = [];
    private CancellationTokenSource? cts;
    private Microsoft.UI.Dispatching.DispatcherQueue dispatcher = MainWindow.Instance.DispatcherQueue;
    
    public async Task<bool> StartScrcpy(string? customArgs = null)
    {
        Process? process = null;
        CancellationTokenSource? processCts = null;

        try
        {
            var scrcpyPath = userSettingsService.FeatureSettingsService.ScrcpyPath;
            if (!File.Exists(scrcpyPath))
            {
                logger.Error("Scrcpy not found at {ScrcpyPath}", scrcpyPath);
                await dispatcher.EnqueueAsync(async () =>
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = MainWindow.Instance.Content.XamlRoot,
                        Title = "ScrcpyNotFound".GetLocalizedResource(),
                        Content = "ScrcpyNotFoundDescription".GetLocalizedResource(),
                        PrimaryButtonText = "SelectLocation".GetLocalizedResource(),
                        DefaultButton = ContentDialogButton.Primary,
                        CloseButtonText = "Dismiss".GetLocalizedResource()
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        await SelectScrcpyLocationClick(null, null);
                    }
                });
                return false;
            }

            List<AdbDevice> onlineDevices = [];
            if (adbService.IsMonitoring)
            {
                // Get unique online devices (avoid duplicates by serial)
                onlineDevices = [.. devices.Where(d => d.State == DeviceState.Online)
                                          .GroupBy(d => d.Serial)
                                          .Select(g => g.First())];

                logger.Info(string.Join(", ", onlineDevices.Select(d => $"{d.Serial} - {d.Type} - {d.State}")));
                if (onlineDevices.Count == 0)
                {
                    logger.Warn("No online devices found from adb");
                    var ipAddress = sessionManager.GetConnectedSessionIpAddress();
                    if (!string.IsNullOrEmpty(ipAddress) && !await adbService.ConnectWireless(ipAddress))
                    {
                        logger.Warn("Failed to connect to Adb wirelessly");
                        await dispatcher.EnqueueAsync(async () =>
                        {
                            var dialog = new ContentDialog
                            {
                                XamlRoot = MainWindow.Instance.Content.XamlRoot,
                                Title = "AdbDeviceOffline".GetLocalizedResource(),
                                Content = "AdbDeviceOfflineDescription".GetLocalizedResource(),
                                CloseButtonText = "Dismiss".GetLocalizedResource()
                            };
                            await dialog.ShowAsync();
                        });
                    }
                    return false;
                }
            }

            // Determine if we need to show the device selection dialog
            string? selectedDeviceSerial = null;
            bool shouldShowDialog = ShouldShowDeviceSelectionDialog(onlineDevices);
            
            if (shouldShowDialog)
            {
                selectedDeviceSerial = await ShowDeviceSelectionDialog(onlineDevices);
                if (selectedDeviceSerial == null)
                {
                    logger.Info("User canceled device selection");
                    return false;
                }
            }

            // Build arguments for scrcpy with the selected device
            var args = BuildScrcpyArguments(customArgs, selectedDeviceSerial);
            
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
            
            // Start monitoring process in background
            StartProcessMonitoring(process, processCts);
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

    private void StartProcessMonitoring(Process process, CancellationTokenSource processCts)
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
        logger.Info($"scrcpy process started (PID: {process.Id})");
        
        lock (scrcpyProcesses)
        {
            scrcpyProcesses.Add(process);
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await process.WaitForExitAsync(processCts.Token);
                logger.Info($"scrcpy process exited with code {process.ExitCode}");
                
                if (process.ExitCode != 0)
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
                            XamlRoot = MainWindow.Instance.Content.XamlRoot,
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
                            logger.Info("Scrcpy error output copied to clipboard");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                if (!(ex is OperationCanceledException))
                {
                    logger.Error($"Error monitoring scrcpy process", ex);
                }
            }
            finally
            {
                process.Dispose();
                lock (scrcpyProcesses)
                {
                    scrcpyProcesses.Remove(process);
                }
                processCts.Dispose();
                if (ReferenceEquals(cts, processCts))
                {
                    cts = null;
                }
            }
        }, processCts.Token);
    }

    private bool ShouldShowDeviceSelectionDialog(List<AdbDevice> onlineDevices)
    {
        // Always show dialog if selection preference is set to AskEverytime
        if (userSettingsService.FeatureSettingsService.ScrcpyDevicePreference == ScrcpyDevicePreferenceType.AskEverytime)
            return true;
            
        // Always show dialog if there are more than 1 device
        if (onlineDevices.Count <= 1)
            return false;
        
        // Check if there are devices with different models
        var distinctModels = onlineDevices.Select(d => d.Model).Distinct().Count();
        if (distinctModels > 1)
            return true;
        
        // Check if there are multiple WiFi devices of the same model
        var wifiDevices = onlineDevices.Where(d => d.Type == DeviceType.WIFI).ToList();
        if (wifiDevices.Count > 1)
            return true;
        
        // Otherwise, no need to show the dialog
        return false;
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
                XamlRoot = MainWindow.Instance.Content.XamlRoot,
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

    private string BuildScrcpyArguments(string? customArgs = null, string? deviceSerial = null)
    {
        var args = new List<string>();
        
        var preDefinedArgs = userSettingsService.FeatureSettingsService.CustomArguments;
        
        // Add custom arguments if provided 
        if (!string.IsNullOrEmpty(customArgs))
        {
            args.Add(customArgs);
        }

        if (!string.IsNullOrEmpty(preDefinedArgs))
        {
            args.Add(preDefinedArgs);
        }
        
        // Add device serial if provided
        if (!string.IsNullOrEmpty(deviceSerial))
        {
            args.Add($"-s {deviceSerial}");
        }
        
        // Get feature settings
        var settings = userSettingsService.FeatureSettingsService;
       

        // General settings
        if (settings.ScreenOff)
        {
            args.Add("--turn-screen-off");
        }
        
        if (settings.PhysicalKeyboard)
        {
            args.Add("--keyboard=uhid");
        }

        args.Add("--tcpip");
        
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
        
        if (!string.IsNullOrEmpty(settings.VirtualDisplaySize))
        {
            args.Add($"--new-display={settings.VirtualDisplaySize}");
        }
        
        // Audio settings
        if (!string.IsNullOrEmpty(settings.AudioBitrate))
        {
            args.Add($"--audio-bit-rate={settings.AudioBitrate}");
        }
        
        if (!string.IsNullOrEmpty(settings.AudioBuffer))
        {
            args.Add($"--audio-output-buffer={settings.AudioBuffer}");
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

        // Only continue with device selection if the user hasn't already specified a device
        // and we haven't added a device serial above
        bool hasDeviceSelectionFlag = (args.Contains("-s") || args.Contains("-d") || args.Contains("-e"));

        if (devices.Count > 1 && !hasDeviceSelectionFlag && string.IsNullOrEmpty(deviceSerial))
        {
            var deviceSelection = userSettingsService.FeatureSettingsService.ScrcpyDevicePreference;
            
            switch (deviceSelection)
            {
                case ScrcpyDevicePreferenceType.Usb:
                    args.Add("-d");  // Use USB device
                    break;
                case ScrcpyDevicePreferenceType.Tcpip:
                    args.Add("-e");  // Use TCP device
                    break;
                case ScrcpyDevicePreferenceType.Auto:
                default:
                    var usbDevice = devices.FirstOrDefault(d => d.Type == DeviceType.USB && d.State == DeviceState.Online);
                    if (usbDevice != null)
                    {
                        args.Add("-d");  // Use USB device
                    }
                    else
                    {
                        args.Add("-e");  // Use TCP device
                    }
                    break;
            }
        }
        return string.Join(" ", args);
    }

    public async Task SelectScrcpyLocationClick(object sender, RoutedEventArgs e)
    {
        var path = await LocationPicker.FileLocationPicker();

        if (!string.IsNullOrEmpty(path))
        {        
            var viewmodel = Ioc.Default.GetRequiredService<FeaturesViewModel>();
            viewmodel.ScrcpyPath = path;

            var directory = Path.GetDirectoryName(path);
            if(string.IsNullOrEmpty(directory) || !string.IsNullOrEmpty(viewmodel.AdbPath)) return;
            var adbPath = Path.GetFullPath(Path.Combine(directory, "adb.exe"));
            if (File.Exists(adbPath))
            {
                viewmodel.AdbPath = adbPath;
            }
            await StartScrcpy();
            return;
        }
        return;
    }
}
