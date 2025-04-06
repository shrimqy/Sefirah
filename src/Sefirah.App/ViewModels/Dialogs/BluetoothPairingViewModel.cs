namespace Sefirah.App.ViewModels.Dialogs;

public partial class BluetoothPairingViewModel : ObservableObject
{    
    [ObservableProperty]
    private string deviceName;

    [ObservableProperty]
    private string pairingCode;

    public BluetoothPairingViewModel(string deviceName, string pairingCode)
    {
        DeviceName = deviceName;
        PairingCode = pairingCode;
    }
}

