using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Controls;
using Org.BouncyCastle.Crypto.Parameters;
using Sefirah.App.Data.AppDatabase;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;
using Sefirah.App.Dialogs;
using Sefirah.App.Utils;
using Windows.Storage;

namespace Sefirah.App.Services;

public class DeviceManager(DeviceRepository repository, ILogger logger) : IDeviceManager
{
    public event EventHandler<DeviceStatus>? DeviceStatusChanged;
    public DeviceStatus? CurrentDeviceStatus { get; private set; }

    public async Task<List<RemoteDeviceEntity>> GetDeviceListAsync()
    {
        try
        {
            return await repository.GetAllAsync();
        }
        catch (Exception ex)
        {
            logger.Error("Error getting device list", ex);
            return [];
        }
    }

    public async Task<RemoteDeviceEntity?> GetDeviceInfoAsync(string deviceId)
    {
        return await repository.GetByIdAsync(deviceId);
    }

    public async Task RemoveDevice(RemoteDeviceEntity device)
    {
        await repository.DeleteAsync(device.DeviceId);
    }

    public async Task UpdateDevice(RemoteDeviceEntity device)
    {
        await repository.AddOrUpdateAsync(device);
    }

    public async Task<RemoteDeviceEntity?> VerifyDevice(DeviceInfo device)
    {
        try
        {

            var localDevice = await GetLocalDeviceAsync();


            var existingDevice = await repository.GetByIdAsync(device.DeviceId);

            // If device exists and we've already verified it before, validate the proof
            if (existingDevice != null)
            {
                
                if (!EcdhHelper.VerifyProof(existingDevice.SharedSecret!, device.Nonce!, device.Proof!)) { return null; }

                existingDevice.LastConnected = DateTime.Now;
                
                if (!string.IsNullOrEmpty(device.Avatar))
                {
                    existingDevice.WallpaperBytes = Convert.FromBase64String(device.Avatar);
                }

                return await repository.AddOrUpdateAsync(existingDevice);
            }

            // For new devices, show connection request dialog
            var tcs = new TaskCompletionSource<RemoteDeviceEntity?>();

            var sharedSecret = EcdhHelper.DeriveKey(device.PublicKey!, localDevice.PrivateKey);

            if (!EcdhHelper.VerifyProof(sharedSecret, device.Nonce!, device.Proof!)) { return null; }

            await MainWindow.Instance.DispatcherQueue.EnqueueAsync(async () =>
            {
                try
                {
                    var frame = (Frame)MainWindow.Instance.Content;
                    var dialog = new ConnectionRequestDialog(device.DeviceName, sharedSecret, frame)
                    {
                        XamlRoot = MainWindow.Instance.Content.XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    
                    if (result != ContentDialogResult.Primary)
                    {
                        logger.Info("User declined device verification");
                        tcs.SetResult(null);
                        return;
                    }

                    var newDevice = new RemoteDeviceEntity
                    {
                        DeviceId = device.DeviceId,
                        Name = device.DeviceName,
                        LastConnected = DateTime.Now,
                        SharedSecret = sharedSecret,
                        WallpaperBytes = !string.IsNullOrEmpty(device.Avatar) 
                            ? Convert.FromBase64String(device.Avatar) 
                            : null
                    };

                    var savedDevice = await repository.AddOrUpdateAsync(newDevice);
                    tcs.SetResult(savedDevice);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return await tcs.Task;
        }
        catch (Exception ex)
        {
            logger.Error("Error verifying device", ex);
            return null;
        }
    }

    public Task UpdateDeviceStatus(DeviceStatus deviceStatus)
    {
        try
        {
            CurrentDeviceStatus = deviceStatus;
            DeviceStatusChanged?.Invoke(this, deviceStatus);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.Error("Error updating device status", ex);
            return Task.CompletedTask;
        }
    }

    public async Task<RemoteDeviceEntity?> GetLastConnectedDevice()
    {
        return await repository.GetLastConnectedDeviceAsync();
    }

    public async Task<LocalDeviceEntity> GetLocalDeviceAsync()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings?.Values["FirstLaunch"] == null)
            {
                var (firstName, _) = await CurrentUserInformation.GetCurrentUserInfoAsync();
                var keyPair = EcdhHelper.GetKeyPair();
                var localDevice = new LocalDeviceEntity
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    DeviceName = firstName,
                    PublicKey = ((ECPublicKeyParameters)keyPair.Public).Q.GetEncoded(false),
                    PrivateKey = ((ECPrivateKeyParameters)keyPair.Private).D.ToByteArrayUnsigned(),
                };
                localSettings!.Values["FirstLaunch"] = false;
                return await repository.AddOrUpdateLocalDeviceAsync(localDevice);
            }
            else
            {
                return await repository.GetLocalDevice() ?? throw new Exception("Local device not found");
            }
        }
        catch (Exception e)
        {
            logger.Error("Error getting local device", e);
            throw;
        }
    }

    public async Task<byte[]?> GetSharedSecretForLastConnectedDeviceAsync()
    {
        try
        {
            var device = await GetLastConnectedDevice();
            return device?.SharedSecret;
        }
        catch (Exception ex)
        {
            logger.Error("Error retrieving shared secret for last connected device", ex);
            return null;
        }
    }
}