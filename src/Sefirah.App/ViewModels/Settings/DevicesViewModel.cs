using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.Utils.Serialization;
using System.Windows.Input;

namespace Sefirah.App.ViewModels.Settings;

public partial class DevicesViewModel : BaseViewModel
{
    private readonly DispatcherQueue Dispatcher;
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IDiscoveryService DiscoveryService { get; } = Ioc.Default.GetRequiredService<IDiscoveryService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();

    // This collection is for devices you already have connected.
    public ObservableCollection<RemoteDeviceEntity?> PairedDevices { get; } = [];

    // Devices discovered on the WiFi network
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices => DiscoveryService.DiscoveredDevices;

    private RemoteDeviceEntity? _currentlyConnectedDevice;
    public RemoteDeviceEntity? CurrentlyConnectedDevice
    {
        get => _currentlyConnectedDevice;
        private set => SetProperty(ref _currentlyConnectedDevice, value);
    }

    public ICommand RemoveDeviceCommand { get; }

    public DevicesViewModel()
    {
        Dispatcher = DispatcherQueue.GetForCurrentThread();

        RemoveDeviceCommand = new AsyncRelayCommand<RemoteDeviceEntity>(RemoveDevice);
        SessionManager.ClientConnectionStatusChanged += OnConnectionStatusChange;
        LoadDevices();
    }

    private async void LoadDevices()
    {
        var devices = await DeviceManager.GetDeviceListAsync();
        Dispatcher.TryEnqueue(async () =>
        {
            PairedDevices.Clear();
            foreach (var device in devices)
            {

                if (device != null)
                {
                    // Load the images
                    if (device.WallpaperBytes != null && device.WallpaperImage == null)
                    {
                        device.WallpaperImage = await device.WallpaperBytes.ToBitmapAsync();
                    }
                }
                PairedDevices.Add(device);
            }
        });
    }

    private async void OnConnectionStatusChange(object? sender, ConnectedSessionEventArgs args)
    {
        await Dispatcher.EnqueueAsync(() =>
        {
            CurrentlyConnectedDevice = args.Device;
        });
    }

    private async Task RemoveDevice(RemoteDeviceEntity? device)
    {
        if (device == null)
        {
            return;
        }   
        var dialog = new ContentDialog
        {
            Title = "RemoveDeviceDialogTitle".GetLocalizedResource(),
            Content = string.Format("RemoveDeviceDialogSubtitle".GetLocalizedResource(), device.Name),
            PrimaryButtonText = "RemoveDeviceConfirmButton".GetLocalizedResource(),
            CloseButtonText = "RemoveDeviceCancelButton".GetLocalizedResource(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = MainWindow.Instance.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                // First disconnect if this is the currently connected device
                if (CurrentlyConnectedDevice?.DeviceId == device.DeviceId)
                {
                    var message = new Misc { MiscType = nameof(MiscType.Disconnect) };
                    SessionManager.SendMessage(SocketMessageSerializer.Serialize(message));

                    SessionManager.DisconnectSession(true);
                }

                // Remove the device from the database
                await DeviceManager.RemoveDevice(device);
                
                // Remove from the UI collection
                await Dispatcher.EnqueueAsync(() =>
                {
                    PairedDevices.Remove(device);
                });
            }
            catch (Exception ex)
            {
                // Show error dialog
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = $"Failed to remove device: {ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = MainWindow.Instance.Content.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }
    }
}
