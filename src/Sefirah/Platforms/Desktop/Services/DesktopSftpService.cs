using GLib;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Task = System.Threading.Tasks.Task;

namespace Sefirah.Platforms.Desktop.Services;

public class DesktopSftpService(ILogger<DesktopSftpService> logger) : ISftpService
{
    private readonly Dictionary<string, string> _mountedDevices = [];

    public async Task InitializeAsync(PairedDevice device, SftpServerInfo info)
    {
        var sftpUri = $"sftp://{info.IpAddress}:{info.Port}/";
        
        logger.Info($"Initializing SFTP service for device {device.Name}, uri: {sftpUri}");
        try
        {
            var file = FileFactory.NewForUri(sftpUri);

            using var mountOp = new MountOperation
            {
                Username = info.Username,
                Password = info.Password,
                PasswordSave = PasswordSave.Never
            };
            
            // Handle signals
            mountOp.AskPassword += (sender, args) =>
            {
                args.RetVal = MountOperationResult.Handled; 
            };
            
            mountOp.AskQuestion += (sender, args) =>
            {
                args.RetVal = MountOperationResult.Handled;
            };
            
            using var cancellable = new Cancellable();
            
            var tcs = new TaskCompletionSource<bool>();

            file.MountEnclosingVolume(
                MountMountFlags.None,
                mountOp,
                cancellable,
                (sourceObject, res) =>
                {
                    logger.Info("MountEnclosingVolume callback invoked");
                    try
                    {
                        if (sourceObject is IFile fileObj)
                        {
                            var success = fileObj.MountEnclosingVolumeFinish(res);
                            logger.Info($"MountEnclosingVolumeFinish returned: {success}");
                            tcs.SetResult(success);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error in MountEnclosingVolumeFinish callback: {Error}", ex.Message);
                        tcs.SetException(ex);
                    }
                }
            );
            
            logger.Info("Waiting for mount operation to complete...");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await tcs.Task.WaitAsync(cts.Token);
                logger.Info("Mount operation completed");
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                cancellable.Cancel();
                throw new TimeoutException("Mount operation timed out");
            }

            _mountedDevices[device.Id] = sftpUri;
            logger.Info($"Successfully mounted SFTP for device {device.Name}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to mount SFTP for device {device.Name}: {ex.Message}");
        }
    }

    public void Remove(string deviceId)
    {
        if (!_mountedDevices.TryGetValue(deviceId, out var sftpUri))
        {
            logger.Info($"Device {deviceId} is not mounted");
            return;
        }
        
        logger.Info($"Unmounting SFTP for device {deviceId}");
        
        try
        {
            var file = FileFactory.NewForUri(sftpUri);

            using var cancellable = new Cancellable();

            var mount = file.FindEnclosingMount(cancellable);
            if (mount is not null)
            {
                var tcs = new TaskCompletionSource<bool>();
                using var mountOp = new MountOperation();
                
                mount.UnmountWithOperation(
                    MountUnmountFlags.None,
                    mountOp,
                    cancellable,
                    (sourceObject, res) =>
                    {
                        try
                        {
                            var success = mount.UnmountWithOperationFinish(res);
                            tcs.SetResult(success);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }
                );

                tcs.Task.Wait();
                
                logger.Info($"Successfully unmounted SFTP for device {deviceId}");
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error unmounting SFTP for device {deviceId}: {ex.Message}");
        }
        finally
        {
            _mountedDevices.Remove(deviceId);
        }
    }
}
