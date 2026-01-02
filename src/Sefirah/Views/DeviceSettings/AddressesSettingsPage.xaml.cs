using Sefirah.ViewModels.Settings;

namespace Sefirah.Views.DeviceSettings;

public sealed partial class AddressesSettingsPage : Page
{
    public DeviceSettingsViewModel ViewModel
    {
        get => (DeviceSettingsViewModel)DataContext;
        private set => DataContext = value;
    }

    public AddressesSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is DeviceSettingsViewModel viewModel)
        {
            ViewModel = viewModel;
        }
    }

    private void OnAddressEnabledChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.SaveAddresses();
    }
}

