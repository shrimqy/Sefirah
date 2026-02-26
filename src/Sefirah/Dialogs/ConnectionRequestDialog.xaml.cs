using Sefirah.ViewModels.Dialogs;

namespace Sefirah.Dialogs;

public sealed partial class ConnectionRequestDialog : ContentDialog
{
    public ConnectionRequestViewModel ViewModel
    {
        get => (ConnectionRequestViewModel)DataContext;
        private set => DataContext = value;
    }

    public ConnectionRequestDialog(string deviceName, string verificationKey, Frame frame)
    {
        InitializeComponent();
        ViewModel = new ConnectionRequestViewModel(deviceName, verificationKey, frame);
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
