using AdvancedSharpAdbClient.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Services;
public class ScreenMirrorService(
    ILogger logger,
    IUserSettingsService userSettingsService,
    IAdbService adbService
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
                logger.Error("Scrcpy path is not set");
                return false;
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

        if (settings.PreferTcpIp)
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
        bool hasDeviceSelectionFlag = !string.IsNullOrEmpty(customArgs) && 
            (customArgs.Contains(" -s ") || customArgs.Contains(" -d ") || 
             customArgs.Contains(" -e ") || customArgs.StartsWith("-s ") || 
             customArgs.StartsWith("-d ") || customArgs.StartsWith("-e "));
            
        if (devices.Count > 1 && !hasDeviceSelectionFlag)
        {
            var usbDevice = devices.FirstOrDefault(d => d.Type == DeviceType.Usb && d.State == DeviceState.Online);
            if (usbDevice != null && !settings.PreferTcpIp)
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
}
