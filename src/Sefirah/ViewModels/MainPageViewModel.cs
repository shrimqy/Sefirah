using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Dialogs;
using Sefirah.Services;
using Sefirah.Utils.Serialization;

namespace Sefirah.ViewModels;
public sealed partial class MainPageViewModel : BaseViewModel
{
    public IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private INotificationService NotificationService { get; } = Ioc.Default.GetRequiredService<INotificationService>();
    private RemoteAppRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IUpdateService UpdateService { get; } = Ioc.Default.GetRequiredService<IUpdateService>();
    private IFileTransferService FileTransferService { get; } = Ioc.Default.GetRequiredService<IFileTransferService>();

    private ObservableCollection<PairedDevice> PairedDevices => DeviceManager.PairedDevices;

    public ReadOnlyObservableCollection<Notification> Notifications => NotificationService.NotificationHistory;

    public PairedDevice? Device => DeviceManager.ActiveDevice;

    [ObservableProperty]
    public partial bool LoadingScrcpy { get; set; } = false;

    public bool IsUpdateAvailable => UpdateService.IsUpdateAvailable;

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
    public void SwitchToNextDevice(bool scrollDown)
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
        if (scrollDown)
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
    public void RemoveNotification(string notificationKey)
    {
        NotificationService.RemoveNotification(Device!, notificationKey, false);
    }

    [RelayCommand]
    public void HandleNotificationAction(NotificationAction action)
    {
        NotificationService.ProcessClickAction(Device!, action.NotificationKey, action.ActionIndex);
    }

    public void HandleNotificationReply(Notification message, string replyText)
    {
        NotificationService.ProcessReplyAction(Device!, message.Key, message.ReplyResultKey!, replyText);
    }

    public void PinNotification(string notificationKey)
    {
        NotificationService.TogglePinNotification(notificationKey);
    }

    public async void UpdateNotificationFilter(string appPackage)
    {
        await RemoteAppsRepository.AddOrUpdateAppNotificationFilter(deviceId: Device!.Id, appPackage: appPackage, filter: NotificationFilter.Disabled);
    }

    public void UpdateApp()
    {
        UpdateService.DownloadUpdatesAsync();
    }

    public async void SendFiles(StorageFile[] storageFiles)
    {
        PairedDevice? selectedDevice = null;
        if (PairedDevices.Count == 0)
        {
            return;
        }
        else if (PairedDevices.Count == 1)
        {
            selectedDevice = PairedDevices[0];
        }
        else if (PairedDevices.Count > 1)
            selectedDevice = await DeviceSelector.ShowDeviceSelectionDialog(PairedDevices.ToList());
        {
        }
        if (selectedDevice == null)
            return;

        await FileTransferService.SendBulkFiles(storageFiles, selectedDevice);
    }

    public MainPageViewModel()
    {
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IDeviceManager.ActiveDevice))
                OnPropertyChanged(nameof(Device));
        };
    }
}
