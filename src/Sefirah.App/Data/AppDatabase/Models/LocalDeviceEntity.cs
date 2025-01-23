namespace Sefirah.App.Data.AppDatabase.Models;
using System;

public class LocalDeviceEntity : BaseEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public byte[] PublicKey { get; set; } = [];
    public byte[] PrivateKey { get; set; } = [];
}
