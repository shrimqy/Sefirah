using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class DeviceRepository(DatabaseContext context)
{
    public LocalDeviceEntity? GetLocalDevice()
    {
        return context.Database.Table<LocalDeviceEntity>().FirstOrDefault();
    }

    public void AddOrUpdateLocalDevice(LocalDeviceEntity device)
    {
        context.Database.InsertOrReplace(device);
    }

    public void AddOrUpdateRemoteDevice(PairedDeviceEntity device)
    {
        context.Database.InsertOrReplace(device);
    }

    public async Task<PairedDeviceEntity> GetPairedDevice(string deviceId)
    {
        return await Task.Run(() => context.Database.Find<PairedDeviceEntity>(deviceId));
    }
    public async Task<List<PairedDevice>> GetPairedDevices()
    {
        return [.. await Task.WhenAll(context.Database.Table<PairedDeviceEntity>()
            .OrderByDescending(d => d.LastConnected)
            .Select(d => d.ToPairedDevice()))];
    }
        

    public bool DeletePairedDevice(string deviceId)
    {
        var device = context.Database.Find<PairedDeviceEntity>(deviceId);
        if (device is not null)
        {
            return context.Database.Delete(device) > 0;
        }
        return false;
    }

    public List<string> GetRemoteDeviceAddresses()
    {
        return context.Database.Table<PairedDeviceEntity>()
            .SelectMany(d => d.Addresses)
            .Where(ip => ip.IsEnabled)
            .Select(ip => ip.Address)
            .ToList();
    }
}
 
