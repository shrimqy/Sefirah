using Sefirah.Data.Models;
using Sefirah.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Sefirah.Platforms.Desktop.Features;

public class SftpFeature(
    ILogger<SftpFeature> logger,
    IUserSettingsService userSettingsService) : ISftpFeature
{
    private enum MountBackend { Gvfs, Sshfs }

    private readonly record struct DeviceSession(
        string Host,
        SftpServerInfo Info,
        MountBackend? Backend = null,
        string? Location = null);

    private readonly Dictionary<string, DeviceSession> _sessions = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task Mount(PairedDevice device, SftpServerInfo info)
    {
        if (string.IsNullOrEmpty(device.Address)) return;

        // Drop any previous mount for this device
        Remove(device.Id);

        logger.Info($"Mounting SFTP for {device.Name}, IP: {device.Address}, Port: {info.Port}, Username: {info.Username}");

        _sessions[device.Id] = new DeviceSession(device.Address, info);
        SessionChanged?.Invoke(this, device);

        var preferSshfs = ShouldPreferSshfs();
        if (preferSshfs)
        {
            if (await TryMountSshfsAsync(device.Id, device.Address, info))
                return;

            logger.Warn($"sshfs mount failed for {device.Name}, falling back to GVfs");
            await TryMountGvfsAsync(device.Id, device.Address, info);
        }
        else
        {
            if (await TryMountGvfsAsync(device.Id, device.Address, info))
                return;

            logger.Warn($"GVfs mount failed for {device.Name}, falling back to sshfs");
            await TryMountSshfsAsync(device.Id, device.Address, info);
        }
    }

    public async Task BrowseAsync(PairedDevice device)
    {
        if (!_sessions.TryGetValue(device.Id, out var session))
        {
            logger.Warn($"No SFTP session available to browse device {device.Name}");
            return;
        }

        try
        {
            var path = session.Info.Paths.Count == 1 ? session.Info.Paths[0] : "/";

            if (session is { Backend: MountBackend.Sshfs, Location: { } mountPoint })
            {
                var localPath = CombineMountPath(mountPoint, path);
                await Launcher.LaunchUriAsync(new Uri($"file://{localPath}"));
                return;
            }

            var browseUri = SftpUriHelper.CreateGvfsUri(session.Host, session.Info.Port, session.Info.Username, path);
            await Launcher.LaunchUriAsync(new Uri(browseUri));
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to browse device {device.Name}", ex);
        }
    }

    public async Task BrowseUriAsync(PairedDevice device)
    {
        if (!_sessions.TryGetValue(device.Id, out var session))
        {
            logger.Warn($"No SFTP session available to browse device {device.Name}");
            return;
        }

        try
        {
            var path = session.Info.Paths.Count == 1 ? session.Info.Paths[0] : "/";
            var uri = SftpUriHelper.CreateBrowseUri(
                session.Host,
                session.Info.Port,
                session.Info.Username,
                session.Info.Password,
                path);

            var dataPackage = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dataPackage.SetText(uri.AbsoluteUri);
            Clipboard.SetContent(dataPackage);
            await Launcher.LaunchUriAsync(uri);
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to browse device {device.Name} via SFTP URI", ex);
        }
    }

    public event EventHandler<PairedDevice>? SessionChanged;

    public SftpSession? GetSession(string deviceId)
        => _sessions.TryGetValue(deviceId, out var session)
            ? new SftpSession(session.Host, session.Info.Port, session.Info.Username, session.Info.Password, session.Info.Paths, session.Info.PathNames)
            : null;

    public Task RemoveAll()
    {
        foreach (var deviceId in _sessions.Keys.ToList())
            Remove(deviceId);

        return Task.CompletedTask;
    }

    public void Remove(string deviceId)
    {
        if (!_sessions.TryGetValue(deviceId, out var session))
            return;

        if (session is { Backend: { } backend, Location: { } location })
        {
            switch (backend)
            {
                case MountBackend.Gvfs:
                    RunProcess("gio", ["mount", "-u", location]);
                    break;
                case MountBackend.Sshfs:
                    UnmountSshfs(location);
                    break;
            }
        }

        _sessions.Remove(deviceId);
    }

    private async Task<bool> TryMountGvfsAsync(string deviceId, string host, SftpServerInfo info)
    {
        try
        {
            var mountUri = SftpUriHelper.CreateGvfsUri(host, info.Port, info.Username);

            // Android SFTP host keys change often; clear + accept-new mirrors
            RunProcess("ssh-keygen", ["-R", $"[{host}]:{info.Port}"]);
            await AcceptHostKeyAsync(host, info.Port, info.Username);

            var (exitCode, errorOutput) = await RunProcessWithStdinAsync("gio", ["mount", mountUri], info.Password);

            if (exitCode != 0)
            {
                if (errorOutput.Contains("already mounted", StringComparison.OrdinalIgnoreCase))
                {
                    SetMounted(deviceId, MountBackend.Gvfs, mountUri);
                    return true;
                }

                logger.Error($"Failed to mount GVfs SFTP {mountUri}: {errorOutput}");
                return false;
            }

            SetMounted(deviceId, MountBackend.Gvfs, mountUri);
            logger.Info($"Mounted GVfs SFTP: {mountUri}");
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            logger.Warn($"gio/GVfs is not available: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> TryMountSshfsAsync(string deviceId, string host, SftpServerInfo info)
    {
        try
        {
            var mountPoint = GetSshfsMountPoint(deviceId);
            PrepareSshfsMountPoint(mountPoint);

            var remote = $"{info.Username}@{host}:/";
            var (exitCode, errorOutput) = await RunProcessWithStdinAsync(
                "sshfs",
                [
                    remote,
                    mountPoint,
                    "-p", info.Port.ToString(),
                    "-s",
                    "-o", "StrictHostKeyChecking=no",
                    "-o", "UserKnownHostsFile=/dev/null",
                    "-o", "reconnect",
                    "-o", "ServerAliveInterval=30",
                    "-o", "password_stdin"
                ],
                info.Password);

            if (exitCode != 0)
            {
                logger.Error($"Failed to mount sshfs SFTP {remote}: {errorOutput}");
                try { Directory.Delete(mountPoint); } catch { /* ignore */ }
                return false;
            }

            SetMounted(deviceId, MountBackend.Sshfs, mountPoint);
            logger.Info($"Mounted sshfs SFTP at {mountPoint}");
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            logger.Warn($"sshfs is not available: {ex.Message}");
            return false;
        }
    }

    private void SetMounted(string deviceId, MountBackend backend, string location)
    {
        if (!_sessions.TryGetValue(deviceId, out var session))
            return;

        _sessions[deviceId] = session with { Backend = backend, Location = location };
    }

    private static string GetSshfsMountPoint(string deviceId)
    {
        // disposable path under the user runtime dir, keyed by device id.
        var runtimeDir = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (string.IsNullOrEmpty(runtimeDir) || !Directory.Exists(runtimeDir))
            runtimeDir = Path.GetTempPath();

        var safeId = string.Concat(deviceId.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        return Path.Combine(runtimeDir, $"sefirah-sftp-{safeId}");
    }

    private static void UnmountSshfs(string mountPoint)
    {
        if (RunProcess("fusermount3", ["-u", mountPoint]) != 0)
            RunProcess("fusermount", ["-u", mountPoint]);

        try
        {
            if (Directory.Exists(mountPoint) && !Directory.EnumerateFileSystemEntries(mountPoint).Any())
                Directory.Delete(mountPoint);
        }
        catch
        {
            // Best-effort cleanup of the empty mount point.
        }
    }

    private static void PrepareSshfsMountPoint(string mountPoint)
    {
        // Clear a stale FUSE mount if a previous run left one behind.
        UnmountSshfs(mountPoint);

        if (Directory.Exists(mountPoint))
        {
            try { Directory.Delete(mountPoint, recursive: false); }
            catch
            {
                // Still mounted or not empty — try again after unmount.
            }
        }

        Directory.CreateDirectory(mountPoint);
        File.SetUnixFileMode(mountPoint, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static string CombineMountPath(string mountPoint, string remotePath)
    {
        var relative = remotePath.TrimStart('/');
        return string.IsNullOrEmpty(relative) ? mountPoint : Path.Combine(mountPoint, relative);
    }

    private bool ShouldPreferSshfs()
    {
        return userSettingsService.GeneralSettingsService.StorageMountPreference switch
        {
            StorageMountPreference.Sshfs => true,
            StorageMountPreference.Gvfs => false,
            _ => IsKdePlasmaDesktop()
        };
    }

    private static bool IsKdePlasmaDesktop()
    {
        var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP")
            ?? Environment.GetEnvironmentVariable("DESKTOP_SESSION")
            ?? string.Empty;

        return desktop.Contains("KDE", StringComparison.OrdinalIgnoreCase)
            || desktop.Contains("Plasma", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AcceptHostKeyAsync(string host, int port, string username)
    {
        await RunProcessWithStdinAsync(
            "ssh",
            [
                "-o", "StrictHostKeyChecking=accept-new",
                "-o", "BatchMode=yes",
                "-o", "ConnectTimeout=5",
                "-p", port.ToString(),
                $"{username}@{host}",
                "true"
            ],
            stdin: null);
    }

    private static int RunProcess(string fileName, IEnumerable<string> arguments)
    {
        var psi = CreateStartInfo(fileName, arguments);
        using var process = Process.Start(psi);
        if (process is null)
            return -1;

        process.WaitForExit(5_000);
        return process.HasExited ? process.ExitCode : -1;
    }

    private static async Task<(int ExitCode, string ErrorOutput)> RunProcessWithStdinAsync(
        string fileName,
        IEnumerable<string> arguments,
        string? stdin)
    {
        var psi = CreateStartInfo(fileName, arguments);
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        using var process = Process.Start(psi);
        if (process is null)
            return (-1, "Failed to start process");

        if (stdin is not null)
        {
            await process.StandardInput.WriteLineAsync(stdin);
            await process.StandardInput.FlushAsync();
        }
        process.StandardInput.Close();

        var errorOutput = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, errorOutput);
    }

    private static ProcessStartInfo CreateStartInfo(string fileName, IEnumerable<string> arguments)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            psi.ArgumentList.Add(argument);

        return psi;
    }
}
