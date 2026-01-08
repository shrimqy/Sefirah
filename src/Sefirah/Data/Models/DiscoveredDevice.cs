using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Helpers;

namespace Sefirah.Data.Models;

public partial class DiscoveredDevice : BaseRemoteDevice
{
    public int Port { get; set; }

    public bool IsPairing { get; set; }

    public byte[] SharedSecret { get; set; } = [];
    
    public string VerificationKey => EcdhHelper.FormatSharedSecret(SharedSecret);

    internal PairedDevice ToPairedDevice()
    {
        return new PairedDevice(Id)
        {
            Name = Name,
            Model = Model,
            Session = Session,
            Client = Client,
            Addresses = [new AddressEntry { Address = Address, IsEnabled = true, Priority = 0 }],
            ConnectionStatus = new Connected(),
        };
    }

    internal RemoteDeviceEntity ToDeviceEntity()
    {
        return new RemoteDeviceEntity
        {
            DeviceId = Id,
            Name = Name,
            LastConnected = DateTime.Now,
            Model = Model,
            SharedSecret = SharedSecret,
            WallpaperBytes = null,
            Addresses = [new AddressEntry { Address = Address, IsEnabled = true, Priority = 0 }],
            PhoneNumbers = []
        };
    }

}

