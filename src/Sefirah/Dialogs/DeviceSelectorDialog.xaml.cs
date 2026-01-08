using Sefirah.Data.Models;
using Sefirah.ViewModels.Dialogs;

namespace Sefirah.Dialogs;

public sealed partial class DeviceSelectorDialog : UserControl
{
    public DeviceSelectorViewModel ViewModel
    {
        get => (DeviceSelectorViewModel)DataContext;
        private set => DataContext = value;
    }

    public DeviceSelectorDialog(List<PairedDevice> devices)
    {
        InitializeComponent();
        ViewModel = new DeviceSelectorViewModel(devices);
    }

    private void DeviceCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is PairedDevice device)
        {
            ViewModel.SetDeviceSelected(device, true);
        }
    }

    private void DeviceCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is PairedDevice device)
        {
            ViewModel.SetDeviceSelected(device, false);
        }
    }
}
