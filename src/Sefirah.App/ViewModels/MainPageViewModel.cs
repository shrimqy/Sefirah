using CommunityToolkit.WinUI;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.Utils.Serialization;
using System.Windows.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace Sefirah.App.ViewModels;

public sealed class MainPageViewModel : BaseViewModel
{
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private IRemoteAppsRepository remoteAppsRepository { get; } = Ioc.Default.GetRequiredService<IRemoteAppsRepository>();
    private INotificationService NotificationService { get; } = Ioc.Default.GetRequiredService<INotificationService>();

    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();

    private RemoteDeviceEntity? _deviceInfo = new();
    private DeviceStatus _deviceStatus = new();
    private bool _connectionStatus = false;
    public ReadOnlyObservableCollection<Notification> RecentNotifications => NotificationService.NotificationHistory;
    public ObservableCollection<DiscoveredDevice> DiscoveredDevices { get; } = [];

    public RemoteDeviceEntity? DeviceInfo
    {
        get => _deviceInfo;
        set => SetProperty(ref _deviceInfo, value);
    }

    public DeviceStatus DeviceStatus
    {
        get => _deviceStatus;
        set => SetProperty(ref _deviceStatus, value);
    }

    public bool ConnectionStatus
    {
        get => _connectionStatus;
        set
        {
            if (SetProperty(ref _connectionStatus, value))
            {
                OnPropertyChanged(nameof(ConnectionButtonText));
            }
        }
    }

    public string ConnectionButtonText => ConnectionStatus ? "Connected/Text".GetLocalizedResource() : "Disconnected/Text".GetLocalizedResource();

    public ICommand ToggleConnectionCommand { get; }
    public ICommand ClearAllNotificationsCommand { get; }
    public ICommand NotificationActionCommand { get; }
    public ICommand NotificationReplyCommand { get; }
    public ICommand SetRingerModeCommand { get; }
    public ICommand ToggleScreenMirrorCommand { get; }
    public MainPageViewModel()
    {
        try
        {
            // Initialize commands
            ToggleConnectionCommand = new RelayCommand(ToggleConnection);
            ClearAllNotificationsCommand = new RelayCommand(ClearAllNotifications);
            NotificationActionCommand = new RelayCommand<NotificationAction>(HandleNotificationAction);
            NotificationReplyCommand = new RelayCommand<(Notification, string)>(HandleNotificationReply);
            SetRingerModeCommand = new RelayCommand<string>(SetRingerMode);
            ToggleScreenMirrorCommand = new RelayCommand(ToggleScreenMirror);
            getLastConnectedDevice();

            // Subscribe to device events
            SessionManager.ClientConnectionStatusChanged += OnConnectionStatusChange;
            DeviceManager.DeviceStatusChanged += OnDeviceStatusReceived;
        }
        catch (Exception ex)
        {
            logger.Error($"Critical error in MainPageViewModel initialization: {ex}");
            throw;
        }
    }

    private async void OnConnectionStatusChange(object? sender, ConnectedSessionEventArgs args)
    {
        await dispatcher.EnqueueAsync(async () =>
        {
            // If the connection is re-established
            if (args.IsConnected && !_connectionStatus)
            {
                await NotificationService.ClearHistory();
            }

            // Update connection status and device info
            ConnectionStatus = args.IsConnected;
            
            if (args.Device != null)
            {
                // Ensure the new device has its image loaded
                if (args.Device.WallpaperBytes != null && args.Device.WallpaperImage == null)
                {
                    args.Device.WallpaperImage = await args.Device.WallpaperBytes.ToBitmapAsync();
                }
                DeviceInfo = args.Device;
            }
            else
            {
                DeviceInfo = null;
            }
        });
    }

    private async void ToggleScreenMirror()
    {
        var argsTextBox = new TextBox
        {
            PlaceholderText = "Enter command arguments",
            AcceptsReturn = false,
            Width = 300,
            Margin = new Thickness(0, 10, 0, 0)
        };
        
        var panel = new StackPanel();
        panel.Children.Add(argsTextBox);
        
        var dialog = new ContentDialog
        {
            XamlRoot = MainWindow.Instance.Content.XamlRoot,
            Title = "scrcpy",
            Content = panel,
            PrimaryButtonText = "Start",
            CloseButtonText = "Cancel"
        };
        
        var result = await dialog.ShowAsync();
        
        if (result == ContentDialogResult.Primary)
        {
            string args = argsTextBox.Text?.Trim() ?? string.Empty;
            await ScreenMirrorService.StartScrcpy(customArgs: args);
        }
    }

    public async Task OpenApp(string appPackage)
    {
        await ScreenMirrorService.StartScrcpy(customArgs: $"--new-display --start-app={appPackage}");
    }

    private async void getLastConnectedDevice()
    {
        try 
        {
            var lastConnectedDevice = await DeviceManager.GetLastConnectedDevice();
            if (lastConnectedDevice != null)
            {
                await dispatcher.EnqueueAsync(async () =>
                {
                    if (lastConnectedDevice.WallpaperBytes != null && lastConnectedDevice.WallpaperImage == null)
                    {
                        lastConnectedDevice.WallpaperImage = await lastConnectedDevice.WallpaperBytes.ToBitmapAsync();
                    }
                    DeviceInfo = lastConnectedDevice;
                });
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error loading last connected device: {ex}");
        }
    }

    private void ClearAllNotifications()
    {
        NotificationService.ClearAllNotification();
    }

    private async void ToggleConnection()
    {
        if (ConnectionStatus)
        {
            var message = new Misc { MiscType = nameof(MiscType.Disconnect) };
            SessionManager.SendMessage(SocketMessageSerializer.Serialize(message));
            await Task.Delay(50);
            SessionManager.DisconnectSession();
        }
    }

    public async Task UpdateNotificationFilterAsync(string appPackageName, NotificationFilter filter)
    {
        await remoteAppsRepository.UpdateFilterAsync(appPackageName, filter);
    }

    public async Task PinNotificationAsync(string notificationKey)
    {
        await NotificationService.TogglePinNotification(notificationKey);
    }

    private void OnDeviceStatusReceived(object? sender, DeviceStatus deviceStatus)
    {
        dispatcher.TryEnqueue(() => DeviceStatus = deviceStatus);
    }

    public void RemoveNotification(string notificationKey)
    {
        NotificationService.RemoveNotification(notificationKey, false);
    }

    public void Cleanup()
    {
        SessionManager.ClientConnectionStatusChanged -= OnConnectionStatusChange;
        DeviceManager.DeviceStatusChanged -= OnDeviceStatusReceived;
    }

    private void HandleNotificationAction(NotificationAction? action)
    {
        if (action == null) return;
        NotificationService.ProcessClickAction(action.NotificationKey, action.ActionIndex);

    }

    private void HandleNotificationReply((Notification message, string replyText) reply)
    {
        var (message, replyText) = reply;
        NotificationService.ProcessReplyAction(message.Key, message.ReplyResultKey!, replyText);
    }

    private void SetRingerMode(string? modeStr)
    {
        if (int.TryParse(modeStr, out int mode))
        {
            var message = new DeviceRingerMode { RingerMode = mode };
            SessionManager.SendMessage(SocketMessageSerializer.Serialize(message));
        }
    }
}