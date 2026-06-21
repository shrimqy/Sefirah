using Sefirah.Data.Models;
using Sefirah.ViewModels.Dialogs;

namespace Sefirah.Dialogs;

public sealed partial class BluetoothSetupDialog : ContentDialog
{
    public BluetoothSetupViewModel ViewModel { get; }

    public BluetoothSetupDialog(PairedDevice phone)
    {
        ViewModel = new BluetoothSetupViewModel(phone, Hide);
        InitializeComponent();
        Root.DataContext = ViewModel;
    }

    private void CloseDialogButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CancelCommand.Execute(null);
    }
}
