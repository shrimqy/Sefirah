using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Platforms.Desktop.Features;

public class SftpFeature(ILogger<SftpFeature> logger) : ISftpFeature
{
    private readonly Dictionary<string, string> _mountedDevices = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task InitializeAsync(PairedDevice device, SftpServerInfo info)
    {
        if (string.IsNullOrEmpty(device.Address))
        {
            logger.Warn($"Cannot initialize SFTP service for {device.Name}: device has no address");
            return;
        }

        logger.Info($"Initializing SFTP service for device {device.Name}, IP: {device.Address}, Port: {info.Port}");

        var sftpUri = $"sftp://{info.Username}@{device.Address}:{info.Port}/";
        
        logger.Info($"Mounting SFTP for device {device.Name}");

        ProcessExecutor.ExecuteProcess("gio", $"mount -s \"{sftpUri}\"");

        // Use gio mount with password input via stdin
        var (exitCode, errorOutput) = await ExecuteProcessWithPasswordAsync("gio", $"mount \"{sftpUri}\"", info.Password);
        
        if (exitCode != 0)
        {
            logger.Error($"Failed to mount SFTP for device {device.Name}: {errorOutput}");
            return;
        }
        
        _mountedDevices[device.Id] = sftpUri;
        logger.Info($"Successfully mounted SFTP for device {device.Name}");
    }

    private static async Task<(int ExitCode, string ErrorOutput)> ExecuteProcessWithPasswordAsync(string fileName, string arguments, string password)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            RedirectStandardInput = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            return (-1, "Failed to start process");
        }

        // Send password to stdin when prompted
        await process.StandardInput.WriteLineAsync(password);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        await process.WaitForExitAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();
        
        return (process.ExitCode, errorOutput);
    }

    public void RemoveAll() { }

    public void Remove(string deviceId)
    {
        if (!_mountedDevices.TryGetValue(deviceId, out var sftpUri))
        {
            logger.Debug($"Device {deviceId} is not mounted");
            return;
        }
        
        logger.Info($"Unmounting SFTP for device {deviceId}");
        ProcessExecutor.ExecuteProcess("gio", $"mount -u \"{sftpUri}\"");
        _mountedDevices.Remove(deviceId);
    }
}
