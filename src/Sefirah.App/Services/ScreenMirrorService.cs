using Sefirah.App.Data.Contracts;

namespace Sefirah.App.Services;
public class ScreenMirrorService(
    ILogger logger,
    IUserSettingsService userSettingsService
) : IScreenMirrorService
{
    private Process? _scrcpyProcess;
    private CancellationTokenSource? _cts;
    
    public bool IsRunning => _scrcpyProcess != null && !_scrcpyProcess.HasExited;
    
    public async Task<bool> StartScrcpy(
        string? deviceId = null, 
        bool wireless = false,
        string? customArgs = null)
    {
        try
        {
            if (IsRunning)
            {
                logger.Warn("Scrcpy is already running");
                return false;
            }
            
            
            var scrcpyPath = userSettingsService.FeatureSettingsService.ScrcpyPath;
            if (string.IsNullOrEmpty(scrcpyPath))
            {
                logger.Error("Scrcpy path is not set");
                return false;
            }
            
            // Build arguments for scrcpy
            var args = BuildScrcpyArguments(deviceId, wireless, customArgs);
            
            _cts = new CancellationTokenSource();
            
            return await Task.Run(() =>
            {
                _scrcpyProcess = new Process
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
                
                _scrcpyProcess.OutputDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logger.Info($"scrcpy: {e.Data}");
                };
                
                _scrcpyProcess.ErrorDataReceived += (sender, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        logger.Error($"scrcpy error: {e.Data}");
                };
                
                _scrcpyProcess.Exited += (sender, e) =>
                {
                    logger.Info($"scrcpy process exited with code {_scrcpyProcess.ExitCode}");
                    _scrcpyProcess?.Dispose();
                    _scrcpyProcess = null;
                    _cts?.Dispose();
                    _cts = null;
                };
                
                bool started = _scrcpyProcess.Start();
                if (started)
                {
                    _scrcpyProcess.BeginOutputReadLine();
                    _scrcpyProcess.BeginErrorReadLine();
                    logger.Info("scrcpy process started successfully");
                }
                else
                {
                    logger.Error("Failed to start scrcpy process");
                }
                
                return started;
            }, _cts.Token);
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
            if (!IsRunning)
            {
                logger.Warn("No scrcpy process is currently running");
                return true;
            }
            
            _cts?.Cancel();
            
            if (_scrcpyProcess != null && !_scrcpyProcess.HasExited)
            {
                _scrcpyProcess.Kill(true);
                await _scrcpyProcess.WaitForExitAsync();
                _scrcpyProcess.Dispose();
                _scrcpyProcess = null;
            }
            
            _cts?.Dispose();
            _cts = null;
            
            logger.Info("scrcpy process stopped successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Error stopping scrcpy process", ex);
            return false;
        }
    }

    private string BuildScrcpyArguments(string? deviceId, bool wireless, string? customArgs)
    {
        var args = new List<string>();
        
        // Add custom arguments if provided 
        if (!string.IsNullOrEmpty(customArgs))
        {
            args.Add(customArgs);
        }
        
        // Add device selection if specified
        if (!string.IsNullOrEmpty(deviceId))
        {
            args.Add($"--serial={deviceId}");
        }
        
        // Get feature settings
        var settings = userSettingsService.FeatureSettingsService;
        
        // Add settings-based arguments
        
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
        
        // Device control settings
        if (settings.ScreenOff)
        {
            args.Add("--turn-screen-off");
        }
        
        if (settings.PhysicalKeyboard)
        {
            args.Add("--keyboard=uhid");
        }
        
        //args.Add("--stay-awake"); // Keep the device awake
        args.Add("--show-touches"); // Show taps on screen

        if (wireless)
        {
            // TODO: Implement wireless connection
        }
        
        return string.Join(" ", args);
    }
}
