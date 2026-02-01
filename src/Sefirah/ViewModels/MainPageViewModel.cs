using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils;

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
    public PairedDevice? Device => DeviceManager.ActiveDevice;

    [ObservableProperty]
    public partial bool LoadingScrcpy { get; set; } = false;

    public bool IsUpdateAvailable => UpdateService.IsUpdateAvailable;

    /// <summary>
    /// Active device's notifications
    /// </summary>
    public ObservableCollection<Notification> Notifications => NotificationService.Notifications;
    #endregion

    #region Commands


    [RelayCommand]
    public void ToggleConnection()
    {
        if (Device!.IsConnected)
        {
            SessionManager.DisconnectDevice(Device, true);
        }
        else
        {
            SessionManager.ConnectTo(Device);
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
    public void ToggleDnd()
    {
        var message = new DndStatus { IsEnabled = !Device!.DndEnabled };
        Device.SendMessage(message);
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

    [RelayCommand]
    public void OpenDeviceSettings()
    {
        App.OpenDeviceSettingsWindow(Device!);
    }

    #endregion

    #region Methods

    public async Task OpenApp(Notification notification)
    {
        var notificationToInvoke = new NotificationMessage
        {
            NotificationKey = notification.Key,
            NotificationType = NotificationType.Invoke
        };
        string? appIcon = string.Empty;
        if (!string.IsNullOrEmpty(notification.AppPackage))
        {
            appIcon = IconUtils.GetAppIconFilePath(notification.AppPackage);
        }
        var started = await ScreenMirrorService.StartScrcpy(Device!, $"--new-display --start-app={notification.AppPackage}", appIcon);

        // Scrcpy doesn't have a way of opening notifications afaik, so we will just have the notification listener on Android to open it for us
        // Plus we have to wait (2s will do ig?) until the app is actually launched to send the intent for launching the notification since Google added a lot more restrictions in this particular case
        if (started && Device!.IsConnected)
        {
            await Task.Delay(2000);
            Device.SendMessage(notificationToInvoke);
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
        FileTransferService.SendFilesWithPicker(storageItems);
    }

    public void HandleNotificationReply(Notification notification, string replyText)
    {
        NotificationService.ProcessReplyAction(Device!, notification.Key, notification.ReplyResultKey!, replyText);
    }

    public void SetRingerMode(int mode)
    {
        var message = new RingerMode { Mode = mode };
        Device!.SendMessage(message);
    }

    public void SetAudioLevel(AudioStreamType streamType, int level)
    {
        var message = new AudioStreamMessage
        {
            StreamType = streamType,
            Level = level
        };
        Device!.SendMessage(message);
    }

    public void HandlePlaybackAction(MediaSession session, PlaybackActionType actionType, double? value = null)
    {
        if (Device is null || string.IsNullOrEmpty(session.Source)) return;

        var playbackAction = new PlaybackAction
        {
            PlaybackActionType = actionType,
            Source = session.Source,
            Value = value
        };
        Device.SendMessage(playbackAction);
    }

    #endregion

    public MainPageViewModel()
    {
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(IDeviceManager.ActiveDevice))
                OnPropertyChanged(nameof(Device));
        };
    }
}
