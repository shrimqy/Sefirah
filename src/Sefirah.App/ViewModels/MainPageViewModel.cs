using CommunityToolkit.WinUI;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Sefirah.App.Data.EventArguments;
using Sefirah.App.Data.Models;
using Sefirah.App.Extensions;
using Sefirah.App.Utils.Serialization;
using System.Windows.Input;

namespace Sefirah.App.ViewModels;

public sealed class MainPageViewModel : BaseViewModel
{
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private IRemoteAppsRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<IRemoteAppsRepository>();
    private INotificationService NotificationService { get; } = Ioc.Default.GetRequiredService<INotificationService>();

    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();

    private RemoteDeviceEntity? _deviceInfo = new();
    private DeviceStatus _deviceStatus = new();
    private bool _connectionStatus = false;
    private bool _loadingScrcpy = false;
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

    public bool LoadingScrcpy
    {
        get => _loadingScrcpy;
        set => SetProperty(ref _loadingScrcpy, value);
    }

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

            ConnectionStatus = args.IsConnected;
            
            if (args.Device != null)
            {
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
        try
        {
            LoadingScrcpy = true;
            await ScreenMirrorService.StartScrcpy();
        }
        finally
        {
            await Task.Delay(1000);
            LoadingScrcpy = false;
        }
    }

    public async Task OpenApp(Notification notification)
    {
        var notificationToInvoke = new NotificationMessage
        {
            NotificationType = NotificationType.Invoke,
            NotificationKey = notification.Key,
        };
        
        var started = await ScreenMirrorService.StartScrcpy(customArgs: $"--new-display --start-app={notification.AppPackage}");

        // Scrcpy doesn't have a way of opening notifications afaik, so we will just have the notification listener on Android to open it for us
        // Plus we have to wait (2s will do ig?) until the app is actually launched to send the intent for launching the notification since Google added a lot more restrictions in this particular case
        if (started)
        {
            await Task.Delay(2000);
            SessionManager.SendMessage(SocketMessageSerializer.Serialize(notificationToInvoke));
        }

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
            var message = new Misc { MiscType = MiscType.Disconnect };
            SessionManager.SendMessage(SocketMessageSerializer.Serialize(message));
            await Task.Delay(50);
            SessionManager.DisconnectSession();
        }
    }

    public async Task UpdateNotificationFilterAsync(string appPackageName, NotificationFilter filter)
    {
        await RemoteAppsRepository.UpdateFilterAsync(appPackageName, filter);
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