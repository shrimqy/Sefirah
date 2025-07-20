using Sefirah.ViewModels.Dialogs;

namespace Sefirah.Dialogs;
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

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.OnConnectClick();
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        base.Hide();
    }
}
