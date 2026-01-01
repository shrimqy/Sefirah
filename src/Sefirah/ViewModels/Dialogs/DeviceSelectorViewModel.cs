using Sefirah.Data.Models;

namespace Sefirah.ViewModels.Dialogs;

public partial class DeviceSelectorViewModel : ObservableObject
{
    public ObservableCollection<PairedDevice> Devices { get; } = [];

    public List<PairedDevice> SelectedDevices { get; set; } = [];

    public DeviceSelectorViewModel(List<PairedDevice> devices)
    {
        foreach (var device in devices)
        {
            Devices.Add(device);
        }
    }

    public void SetDeviceSelected(PairedDevice device, bool isSelected)
    {
        if (isSelected)
        {
            SelectedDevices.Add(device);
        }
        else
        {
            SelectedDevices.Remove(device);
        }
    }
}

