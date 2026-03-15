using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

public partial class DiscoveredDevice : BaseRemoteDevice
{
    public int Port { get; set; }

    public bool IsPairing { get; set; }

    public string VerificationKey { get; set; } = "00000000";

    internal PairedDevice ToPairedDevice()
    {
        return new PairedDevice(Id)
        {
            Name = Name,
            Model = Model,
            Certificate = Certificate,
            Session = Session,
            Client = Client,
            Addresses = [new AddressEntry { Address = Address, IsEnabled = true, Priority = 0 }],
            ConnectionStatus = new Connected(),
            Port = Port,
        };
    }
}

