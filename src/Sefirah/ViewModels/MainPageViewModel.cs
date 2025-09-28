using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;

namespace Sefirah.ViewModels;
public sealed partial class MainPageViewModel : BaseViewModel
{
    #region Services
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private INotificationService NotificationService { get; } = Ioc.Default.GetRequiredService<INotificationService>();
    private RemoteAppRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IUpdateService UpdateService { get; } = Ioc.Default.GetRequiredService<IUpdateService>();
    private IFileTransferService FileTransferService { get; } = Ioc.Default.GetRequiredService<IFileTransferService>();
    #endregion

    #region Properties
    public ObservableCollection<PairedDevice> PairedDevices => DeviceManager.PairedDevices;
    public ReadOnlyObservableCollection<Notification> Notifications => NotificationService.NotificationHistory;
    public PairedDevice? Device => DeviceManager.ActiveDevice;

    [ObservableProperty]
    public partial bool LoadingScrcpy { get; set; } = false;

    public bool IsUpdateAvailable => UpdateService.IsUpdateAvailable;
    #endregion

    #region Commands

    [RelayCommand]
    public async Task ToggleConnection(PairedDevice? device)
    {
        if (Device!.ConnectionStatus)
        {
            var message = new CommandMessage { CommandType = CommandType.Disconnect };
            SessionManager.SendMessage(Device.Session!, SocketMessageSerializer.Serialize(message));
            await Task.Delay(50);
            if (Device.Session != null)
            {
                SessionManager.DisconnectSession(Device.Session);
            }
            Device.ConnectionStatus = false;
        }
    }

    [RelayCommand]
    public async Task StartScrcpy()
    {
        try
        {
            LoadingScrcpy = true;
            await ScreenMirrorService.StartScrcpy(Device!);
        }
        finally
        {
            await Task.Delay(1000);
            LoadingScrcpy = false;
        }
    }

    [RelayCommand]
    public void SwitchToNextDevice(int delta)
    {
        if (PairedDevices.Count <= 1)
            return;

        var currentIndex = -1;
        for (int i = 0; i < PairedDevices.Count; i++)
        {
            if (PairedDevices[i].Id == Device?.Id)
            {
                currentIndex = i;
                break;
            }
        }

        if (currentIndex == -1)
            return;

        int nextIndex;
        if (delta < 0)
        {
            // Move to next device (or loop back to first)
            nextIndex = (currentIndex + 1) % PairedDevices.Count;
        }
        else
        {
            // Move to previous device (or loop to last)
            nextIndex = (currentIndex - 1 + PairedDevices.Count) % PairedDevices.Count;
        }

        DeviceManager.ActiveDevice = PairedDevices[nextIndex];
    }

    [RelayCommand]
    public void SetRingerMode(string? modeStr)
    {
        if (int.TryParse(modeStr, out int mode) && Device?.Session != null)
        {
            var message = new DeviceRingerMode { RingerMode = mode };
            SessionManager.SendMessage(Device.Session, SocketMessageSerializer.Serialize(message));
        }
    }

    [RelayCommand]
    public void ClearAllNotifications()
    {
        NotificationService.ClearAllNotification();
    }

    [RelayCommand]
    public void Update()
    {
        UpdateService.DownloadUpdatesAsync();
    }

    [RelayCommand]
    public void RemoveNotification(Notification notification)
    {
        NotificationService.RemoveNotification(Device!, notification);
    }

    [RelayCommand]
    public void HandleNotificationAction(NotificationAction action)
    {
        NotificationService.ProcessClickAction(Device!, action.NotificationKey, action.ActionIndex);
    }

    #endregion

    #region Methods

    public async Task OpenApp(Notification notification)
    {
        var notificationToInvoke = new NotificationMessage
        {
            NotificationType = NotificationType.Invoke,
            NotificationKey = notification.Key,
        };
        string? appIcon = string.Empty;
        if (!string.IsNullOrEmpty(notification.AppPackage))
        {
            appIcon = RemoteAppsRepository.GetAppIcon(notification.AppPackage);
        }
        var started = await ScreenMirrorService.StartScrcpy(Device!, $"--new-display --start-app={notification.AppPackage}", appIcon);

        // Scrcpy doesn't have a way of opening notifications afaik, so we will just have the notification listener on Android to open it for us
        // Plus we have to wait (2s will do ig?) until the app is actually launched to send the intent for launching the notification since Google added a lot more restrictions in this particular case
        if (started && Device!.Session != null)
        {
            await Task.Delay(2000);
            SessionManager.SendMessage(Device.Session, SocketMessageSerializer.Serialize(notificationToInvoke));
        }
    }

    public void UpdateNotificationFilter(string appPackage)
    {
        RemoteAppsRepository.UpdateAppNotificationFilter(Device!.Id, appPackage, NotificationFilter.Disabled);
    }

    public void ToggleNotificationPin(Notification notification)
    {
        NotificationService.TogglePinNotification(notification);
    }

    public void SendFiles(IReadOnlyList<IStorageItem> storageItems)
    {
        FileTransferService.SendFiles(storageItems);
    }

    public void HandleNotificationReply(Notification notification, string replyText)
    {
        NotificationService.ProcessReplyAction(Device!, notification.Key, notification.ReplyResultKey!, replyText);
    }

    #endregion

    public MainPageViewModel()
    {
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IDeviceManager.ActiveDevice))
                OnPropertyChanged(nameof(Device));
        };
    }
}
