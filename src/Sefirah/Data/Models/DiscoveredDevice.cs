using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

public partial class DiscoveredDevice : BaseRemoteDevice
{
    public string Address { get; set; } = string.Empty;

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
            Address = Address,
            Addresses = [with([new AddressEntry { Address = Address, IsEnabled = true }])],
            ConnectionStatus = new Connected(),
            Port = Port,
        };
    }
}

