using CommunityToolkit.WinUI;
using Org.BouncyCastle.Crypto.Parameters;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Helpers;
using Sefirah.Utils;
using System.Runtime.InteropServices;

namespace Sefirah.Services;

public partial class DeviceManager(ILogger<DeviceManager> logger, DeviceRepository repository) : ObservableObject, IDeviceManager
{
    public ObservableCollection<PairedDevice> PairedDevices { get; set; } = [];
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];

    [ObservableProperty]
    public partial PairedDevice? ActiveDevice { get; set; }

    /// <summary>
    /// Event fired when the active session changes
    /// </summary>
    public event EventHandler<PairedDevice?>? ActiveDeviceChanged;

    /// <summary>
    /// Finds a device session by device ID
    /// </summary>
    public PairedDevice? FindDeviceById(string deviceId) => PairedDevices.FirstOrDefault(device => device.Id == deviceId);
    
    public Task<RemoteDeviceEntity> GetDeviceInfoAsync(string deviceId)
    {
        if (repository.HasDevice(deviceId, out var device))
        {
            return Task.FromResult(device);
        }
        throw new InvalidOperationException($"Device with ID {deviceId} not found");
    }   

    public List<string> GetRemoteDeviceAddresses()
    {
        return repository.GetRemoteDeviceAddresses();
    }

    public async Task<PairedDevice?> GetLastConnectedDevice()
    {
        return await repository.GetLastConnectedDevice();
    }

    public async Task RemoveDevice(PairedDevice device)
    {
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                try
                {
                    ActiveDevice = null;
                    var result = PairedDevices.Remove(device);
                    repository.DeletePairedDevice(device.Id);
                    if (ActiveDevice?.Id == device.Id)
                    {
                        ActiveDevice = PairedDevices.FirstOrDefault();
                    }
                }
                catch (Exception ex)
                {
                }
            });
        }
        catch (COMException ex)
        {
            logger.LogError(ex, "COMException occurred while removing device {DeviceId}", device.Id);
            throw;
        }
    }

    public Task UpdateDevice(RemoteDeviceEntity device)
    {
        throw new NotImplementedException();
    }

    private static RemoteDeviceEntity CreateDeviceEntity(DiscoveredDevice device)
    {
        return new RemoteDeviceEntity
        {
            DeviceId = device.Id,
            Name = device.Name,
            LastConnected = DateTime.Now,
            Model = device.Model,
            SharedSecret = device.SharedSecret,
            WallpaperBytes = null,
            Addresses = [new AddressEntry { Address = device.Address, IsEnabled = true, Priority = 0 }],
            PhoneNumbers = []
        };
    }


    public async Task<LocalDeviceEntity> GetLocalDeviceAsync()
    {
        try
        {
            var localDevice =  repository.GetLocalDevice();
            if (localDevice is null)
            {
                var name = await UserInformation.GetCurrentUserNameAsync();
                var keyPair = EcdhHelper.GetKeyPair();
                localDevice = new LocalDeviceEntity
                {
                    DeviceId = Guid.NewGuid().ToString(),
                    DeviceName = name,
                    PublicKey = ((ECPublicKeyParameters)keyPair.Public).Q.GetEncoded(false),
                    PrivateKey = ((ECPrivateKeyParameters)keyPair.Private).D.ToByteArrayUnsigned(),
                };

                repository.AddOrUpdateLocalDevice(localDevice);
            }
            return localDevice;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error getting local device");
            throw;
        }
    }

    public void UpdateLocalDevice(LocalDeviceEntity device)
    {
        try
        {
            repository.AddOrUpdateLocalDevice(device);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating local device");
        }
    }

    public async Task Initialize()
    {
        var pairedDevicesList = await repository.GetPairedDevices();
        PairedDevices = pairedDevicesList.ToObservableCollection();
        ActiveDevice = PairedDevices.FirstOrDefault();
    }

    public async Task UpdateDeviceInfo(PairedDevice device, DeviceInfo deviceInfo)
    {
        var existingDevice = repository.GetRemoteDevice(device.Id);
        existingDevice.Name = deviceInfo.DeviceName;
        existingDevice.PhoneNumbers = deviceInfo.PhoneNumbers;
        if (!string.IsNullOrEmpty(deviceInfo.Avatar))
        {
            existingDevice.WallpaperBytes = Convert.FromBase64String(deviceInfo.Avatar);
        }

        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            device.Name = deviceInfo.DeviceName;
            device.PhoneNumbers = deviceInfo.PhoneNumbers;
            device.Wallpaper = await existingDevice.WallpaperBytes.ToBitmapAsync();
        });

        repository.AddOrUpdateRemoteDevice(existingDevice);
        
    }

    public async Task<PairedDevice> AddDevice(DiscoveredDevice device)
    {
        var newDeviceEntity = CreateDeviceEntity(device);
        repository.AddOrUpdateRemoteDevice(newDeviceEntity);
        return await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            var pairedDevice = device.ToPairedDevice();
            PairedDevices.Add(pairedDevice);
            ActiveDevice = pairedDevice;
            DiscoveredDevices.Remove(device);
            return pairedDevice;
        });
    }
}
