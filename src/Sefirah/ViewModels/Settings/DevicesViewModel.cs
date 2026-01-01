using Microsoft.UI.Dispatching;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Views;

namespace Sefirah.ViewModels.Settings;

public partial class DevicesViewModel : ObservableObject
{
    #region Services
    private readonly DispatcherQueue Dispatcher;
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private ISftpService SftpService { get; } = Ioc.Default.GetRequiredService<ISftpService>();
    private RemoteAppRepository RemoteAppRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    #endregion
    
    public ObservableCollection<PairedDevice> PairedDevices => DeviceManager.PairedDevices;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices => DeviceManager.DiscoveredDevices;

    public DevicesViewModel()
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    [RelayCommand]
    public void OpenDeviceSettings(PairedDevice? device)
    {
        if (device == null) return;
        var settingsWindow = new DeviceSettingsWindow(device);
        settingsWindow.Activate();
    }

    [RelayCommand]
    public async Task RemoveDevice(PairedDevice? device)
    {
        if (device is null)
        {
            return;
        }
        var dialog = new ContentDialog
        {
            Title = "RemoveDeviceDialogTitle".GetLocalizedResource(),
            Content = string.Format("RemoveDeviceDialogSubtitle".GetLocalizedResource(), device.Name),
            PrimaryButtonText = "Remove".GetLocalizedResource(),
            CloseButtonText = "Cancel".GetLocalizedResource(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result is ContentDialogResult.Primary)
        {
            try
            {
                // First disconnect if this is the currently connected device
                if (device.IsConnected)
                {
                    SessionManager.DisconnectDevice(device);
                }

                SftpService.Remove(device.Id);

                RemoteAppRepository.RemoveAllAppsForDeviceAsync(device.Id);

                await DeviceManager.RemoveDevice(device);
            }
            catch (Exception ex)
            {
                // Show error dialog
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to remove device: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = App.MainWindow.Content!.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }
}
