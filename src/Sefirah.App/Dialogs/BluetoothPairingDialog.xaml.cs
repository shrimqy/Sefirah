using Microsoft.UI.Xaml.Controls;
using Sefirah.App.ViewModels.Dialogs;

namespace Sefirah.App.Dialogs;

public sealed partial class BluetoothPairingDialog : ContentDialog
{
    public BluetoothPairingViewModel ViewModel
    {
        get => (BluetoothPairingViewModel)DataContext;
        private set => DataContext = value;
    }

    public BluetoothPairingDialog(string deviceName, string pairingCode)
    {
        InitializeComponent();
        ViewModel = new BluetoothPairingViewModel(deviceName, pairingCode);
    }
}
