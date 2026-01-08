using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class DeviceRepository(DatabaseContext context, ILogger logger)
{
    public LocalDeviceEntity? GetLocalDevice()
    {
        try
        {
            return context.Database.Table<LocalDeviceEntity>().FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to get local devices {ex}", ex);
            return null;
        }
    }

    public void AddOrUpdateLocalDevice(LocalDeviceEntity device)
    {
        try
        {
            context.Database.InsertOrReplace(device);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to add local device {ex}", ex);
        }
    }

    public void AddOrUpdateRemoteDevice(RemoteDeviceEntity device)
    {
        context.Database.InsertOrReplace(device);
    }

    public bool HasDevice(string deviceId, out RemoteDeviceEntity device)
    {
        device = context.Database.Find<RemoteDeviceEntity>(deviceId);
        return device != null;
    }

    public RemoteDeviceEntity GetRemoteDevice(string deviceId)
    {
        return context.Database.Find<RemoteDeviceEntity>(deviceId);
    }

    public async Task<PairedDevice?> GetLastConnectedDevice()
    {
        try
        {
            var device = await Task.FromResult(context.Database.Table<RemoteDeviceEntity>().OrderByDescending(d => d.LastConnected).FirstOrDefault());
            if (device is null) return null;
            return await device.ToPairedDevice();
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to get last connected device {ex}", ex);
            return null;
        }
    }

    public async Task<List<PairedDevice>> GetPairedDevices()
    {
        try
        {
            var devices = context.Database.Table<RemoteDeviceEntity>()
                .OrderByDescending(d => d.LastConnected)
                .ToList();
            var pairedDevices = await Task.WhenAll(devices.Select(d => d.ToPairedDevice()));
            return pairedDevices.ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get paired devices");
            return [];
        }
    }

    public void DeletePairedDevice(string deviceId)
    {
        var device = context.Database.Find<RemoteDeviceEntity>(deviceId);
        if (device != null)
        {
            context.Database.Delete(device);
        }
    }

    public List<string> GetRemoteDeviceAddresses()
    {
        return context.Database.Table<RemoteDeviceEntity>()
            .SelectMany(d => d.Addresses)
            .Where(ip => ip.IsEnabled)
            .Select(ip => ip.Address)
            .ToList();
    }
}
 
