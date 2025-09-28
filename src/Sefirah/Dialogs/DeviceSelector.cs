using CommunityToolkit.WinUI;
using Sefirah.Data.Models;
using Sefirah.Extensions;

namespace Sefirah.Dialogs;
public static class DeviceSelector
{
    public static async Task<PairedDevice?> ShowDeviceSelectionDialog(List<PairedDevice> onlineDevices)
    {
        PairedDevice? selectedDevice = null;

        await App.MainWindow.DispatcherQueue!.EnqueueAsync(async () =>
        {
            var deviceOptions = new List<ComboBoxItem>();
            foreach (var device in onlineDevices)
            {
                var displayName = device.Name ?? "Unknown";
                var item = new ComboBoxItem
                {
                    Content = $"{displayName}",
                    Tag = device
                };
                deviceOptions.Add(item);
            }

            var deviceSelector = new ComboBox
            {
                ItemsSource = deviceOptions,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                SelectedIndex = 0
            };

            var dialog = new ContentDialog
            {
                XamlRoot = App.MainWindow.Content!.XamlRoot,
                Title = "SelectDevice".GetLocalizedResource(),
                Content = deviceSelector,
                PrimaryButtonText = "Start".GetLocalizedResource(),
                CloseButtonText = "Cancel".GetLocalizedResource(),
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && deviceSelector.SelectedItem is ComboBoxItem selected)
            {
                selectedDevice = selected.Tag as PairedDevice;
            }
        });

        return selectedDevice;
    }
}
