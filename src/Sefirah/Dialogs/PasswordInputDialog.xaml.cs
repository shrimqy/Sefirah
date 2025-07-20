namespace Sefirah.Dialogs;

public sealed partial class PasswordInputDialog : ContentDialog
{
    public string Password => PasswordBox.Password;

    public PasswordInputDialog()
    {
        InitializeComponent();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        base.Hide();
    }
} 
