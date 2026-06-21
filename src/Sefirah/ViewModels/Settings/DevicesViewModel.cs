using Microsoft.UI.Dispatching;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;

namespace Sefirah.ViewModels.Settings;

public partial class DevicesViewModel : ObservableObject
{
    #region Services
    private readonly DispatcherQueue Dispatcher;
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private ISftpService SftpService { get; } = Ioc.Default.GetRequiredService<ISftpService>();
    private RemoteAppRepository RemoteAppRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private SmsRepository SmsRepository { get; } = Ioc.Default.GetRequiredService<SmsRepository>();
    private CallLogRepository CallLogRepository { get; } = Ioc.Default.GetRequiredService<CallLogRepository>();
    private NotificationRepository NotificationRepository { get; } = Ioc.Default.GetRequiredService<NotificationRepository>();
    #endregion
    
    public ObservableCollection<PairedDevice> PairedDevices => DeviceManager.PairedDevices;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices => DeviceManager.DiscoveredDevices;

    public DevicesViewModel()
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();
        DeviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
        UpdateActiveDeviceState(DeviceManager.ActiveDevice);
    }

    private void OnActiveDeviceChanged(object? sender, PairedDevice? activeDevice)
    {
        UpdateActiveDeviceState(activeDevice);
    }

    private void UpdateActiveDeviceState(PairedDevice? activeDevice)
    {
        foreach (var pairedDevice in PairedDevices)
        {
            pairedDevice.IsActiveDevice = pairedDevice == activeDevice;
        }
    }

    [RelayCommand]
    public void OpenDeviceSettings(PairedDevice? device)
    {
        if (device is null) return;
        App.OpenDeviceSettingsWindow(device);
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

                await DeviceManager.RemoveDevice(device);

                SftpService.Remove(device.Id);
                RemoteAppRepository.RemoveAllAppsForDeviceAsync(device.Id);
                SmsRepository.DeleteAllDataForDevice(device.Id);
                CallLogRepository.DeleteAllCallLogsForDevice(device.Id);
                NotificationRepository.RemoveNotificationsForDevice(device.Id);
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

    [RelayCommand]
    public void SelectDevice(PairedDevice? device)
    {
        if (device is null || DeviceManager.ActiveDevice == device)
        {
            return;
        }

        DeviceManager.ActiveDevice = device;
        UpdateActiveDeviceState(device);
    }
}
