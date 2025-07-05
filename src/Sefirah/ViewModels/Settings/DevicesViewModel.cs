using Microsoft.UI.Dispatching;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Extensions;
using Sefirah.Utils.Serialization;
using Sefirah.Views;
using Sefirah.Services;
using CommunityToolkit.WinUI;

namespace Sefirah.ViewModels.Settings;

public partial class DevicesViewModel : ObservableObject
{
    private readonly DispatcherQueue Dispatcher;
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IDiscoveryService DiscoveryService { get; } = Ioc.Default.GetRequiredService<IDiscoveryService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private ISftpService SftpService { get; } = Ioc.Default.GetRequiredService<ISftpService>();
    private IAdbService AdbService { get; } = Ioc.Default.GetRequiredService<IAdbService>();
    
    public ObservableCollection<PairedDevice> PairedDevices => DeviceManager.PairedDevices;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices => DiscoveryService.DiscoveredDevices;

    public DevicesViewModel()
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();
        AdbService.AdbDevices.CollectionChanged += OnAdbDevicesChanged;
    }

    private async void OnAdbDevicesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        try
        {
            if (Dispatcher != null)
            {
                await Dispatcher.EnqueueAsync(() =>
                {
                    foreach (var device in PairedDevices)
                    {
                        device.RefreshAdbStatus();
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating ADB status in DevicesViewModel: {ex.Message}");
        }
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
        if (device == null)
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
            XamlRoot = App.MainWindow!.Content!.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            try
            {
                // First disconnect if this is the currently connected device
                if (device.ConnectionStatus)
                {
                    var message = new CommandMessage { CommandType = CommandType.Disconnect };
                    SessionManager.SendMessage(device.Session!, SocketMessageSerializer.Serialize(message));

                    SessionManager.DisconnectSession(device.Session!);
                }

                SftpService.Remove(device.Id);

                DeviceManager.RemoveDevice(device);
            }
            catch (Exception ex)
            {
                // Show error dialog
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to remove device: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = App.MainWindow!.Content!.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }
}
