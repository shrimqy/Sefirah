using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopSftpService(ILogger<DesktopSftpService> logger) : ISftpService
{
    private readonly Dictionary<string, string> _mountedDevices = [];

    public async Task InitializeAsync(PairedDevice device, SftpServerInfo info)
    {
        logger.LogInformation("Initializing SFTP service for device {DeviceName}, IP: {IpAddress}, Port: {Port}, Password: {Password}", 
            device.Name, info.IpAddress, info.Port, info.Password);

        var sftpUri = $"sftp://{info.Username}@{info.IpAddress}:{info.Port}/";
        
        logger.LogInformation("Mounting SFTP for device {DeviceName}", device.Name);

        ProcessExecutor.ExecuteProcess("gio", $"mount -s \"{sftpUri}\"");

        // Use gio mount with password input via stdin
        var (exitCode, errorOutput) = await ExecuteProcessWithPasswordAsync("gio", $"mount \"{sftpUri}\"", info.Password);
        
        if (exitCode != 0)
        {
            logger.LogError("Failed to mount SFTP for device {DeviceName}: {Error}", device.Name, errorOutput);
            return;
        }
        
        _mountedDevices[device.Id] = sftpUri;
        logger.LogInformation("Successfully mounted SFTP for device {DeviceName}", device.Name);
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

    public void Remove(string deviceId)
    {
        if (!_mountedDevices.TryGetValue(deviceId, out var sftpUri))
        {
            logger.LogDebug("Device {DeviceId} is not mounted", deviceId);
            return;
        }
        
        logger.LogInformation("Unmounting SFTP for device {DeviceId}", deviceId);
        ProcessExecutor.ExecuteProcess("gio", $"mount -u \"{sftpUri}\"");
        _mountedDevices.Remove(deviceId);
    }
}
