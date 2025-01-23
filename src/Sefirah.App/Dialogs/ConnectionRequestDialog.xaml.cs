using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.Enums;
using Sefirah.App.ViewModels.Dialogs;

namespace Sefirah.App.Dialogs;

public sealed partial class ConnectionRequestDialog : ContentDialog
{
    public ConnectionRequestViewModel ViewModel
    {
        get => (ConnectionRequestViewModel)DataContext;
        private set => DataContext = value;
    }

    public ConnectionRequestDialog(string deviceName, byte[] passkey, Frame frame)
    {
        InitializeComponent();
        ViewModel = new ConnectionRequestViewModel(deviceName, passkey, frame);
    }


    private void OnConnectClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.OnConnectClick(sender, args);
    }

    private void OnCancelClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        base.Hide();
    }
}
