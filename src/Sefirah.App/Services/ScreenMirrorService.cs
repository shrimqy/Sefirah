using AdvancedSharpAdbClient.Models;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.ViewModels.Settings;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;

namespace Sefirah.App.Services;
public class ScreenMirrorService(
    ILogger logger,
    IUserSettingsService userSettingsService,
    IAdbService adbService,
    ISessionManager sessionManager
) : IScreenMirrorService
{
    private ObservableCollection<AdbDevice> devices = adbService.Devices;
    private List<Process> scrcpyProcesses = []; // what was this for anyway
    private CancellationTokenSource? cts;
    
    public async Task<bool> StartScrcpy(string? customArgs = null)
    {
        try
        {
            var scrcpyPath = userSettingsService.FeatureSettingsService.ScrcpyPath;
            if (string.IsNullOrEmpty(scrcpyPath))
            {
                await MainWindow.Instance.DispatcherQueue.EnqueueAsync(async () =>
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
                        SelectScrcpyLocation_Click(null, null);
                    }
                });
                return false;
            }

            if (devices.Count == 0 || devices.Any(d => d.State == DeviceState.Offline))
            {
                var ipAddress = sessionManager.GetConnectedSessionIpAddress();
                if (!string.IsNullOrEmpty(ipAddress) && !await adbService.ConnectWireless(ipAddress))
                {
                    logger.Warn("Failed to connect to Adb wirelessly");
                    await MainWindow.Instance.DispatcherQueue.EnqueueAsync(async () =>
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
                    return false;
                }
            }

            // Build arguments for scrcpy
            var args = BuildScrcpyArguments(customArgs);
            
            cts = new CancellationTokenSource();
            
            return await Task.Run(() =>
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(scrcpyPath, "scrcpy.exe"),
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    },
                    EnableRaisingEvents = true
                };
                
                process.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logger.Info($"scrcpy: {e.Data}");
                };
                
                process.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logger.Error($"scrcpy error: {e.Data}");
                };
                
                process.Exited += (sender, e) =>
                {
                    logger.Info($"scrcpy process exited with code {process.ExitCode}");
                    process?.Dispose();
                    cts?.Dispose();
                    cts = null;
                };
                
                bool started = process.Start();
                if (started)
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    logger.Info("scrcpy process started successfully");
                }
                else
                {
                    logger.Error("Failed to start scrcpy process");
                }
                
                return started;
            }, cts.Token);
        }
        catch (Exception ex)
        {
            logger.Error("Error starting scrcpy process", ex);
            return false;
        }
    }
    
    public async Task<bool> StopScrcpy()
    {
        try
        {
            cts?.Cancel();
            
            foreach (var process in scrcpyProcesses)
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(true);
                    await process.WaitForExitAsync();
                    process.Dispose();
                }
            }
            
            cts?.Dispose();
            cts = null;
            
            logger.Info("scrcpy process stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Error stopping scrcpy process", ex);
            return false;
        }
    }

    private string BuildScrcpyArguments(string? customArgs = null)
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
        
        // Get feature settings
        var settings = userSettingsService.FeatureSettingsService;
        
        // Add settings-based arguments

        // General settings
        if (settings.ScreenOff)
        {
            args.Add("--turn-screen-off");
        }
        
        if (settings.PhysicalKeyboard)
        {
            args.Add("--keyboard=uhid");
        }
        var preferTcpIp = settings.PreferTcpIp;
        if (preferTcpIp)
        {
            args.Add("--tcpip");
        }
        
        // Video settings
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
        
        // Audio settings
        if (!string.IsNullOrEmpty(settings.AudioBitrate))
        {
            args.Add($"--audio-bit-rate={settings.AudioBitrate}");
        }
        
        if (!string.IsNullOrEmpty(settings.AudioBuffer))
        {
            args.Add($"--audio-output-buffer={settings.AudioBuffer}");
        }
    
        // Only continue with device selection if the user hasn't already specified a device
        bool hasDeviceSelectionFlag = (args.Contains("-s") || args.Contains("-d") || args.Contains("-e"));

        if (devices.Count > 1 && !hasDeviceSelectionFlag)
        {
            var usbDevice = devices.FirstOrDefault(d => d.Type == DeviceType.Usb && d.State == DeviceState.Online);
            if (usbDevice != null && !preferTcpIp)
            {
                args.Add("-d");  // Use USB device
            }
            else
            {
                args.Add("-e");  // Use TCP device
            }
        }
        
        return string.Join(" ", args);
    }

    public async void SelectScrcpyLocation_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        var window = MainWindow.Instance;
        WinRT.Interop.InitializeWithWindow.Initialize(picker,
            WinRT.Interop.WindowNative.GetWindowHandle(window));
        var viewmodel = Ioc.Default.GetRequiredService<FeaturesViewModel>();
        if (await picker.PickSingleFolderAsync() is StorageFolder folder)
        {
            viewmodel.ScrcpyPath = folder.Path;
            await StartScrcpy();
        }
    }
}
