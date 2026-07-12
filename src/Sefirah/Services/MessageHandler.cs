using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;

namespace Sefirah.Services;
public class MessageHandler(
    RemoteAppRepository remoteAppRepository,
    CallLogRepository callLogRepository,
    ContactRepository contactRepository,
    IDeviceManager deviceManager,
    INotificationFeature notificationFeature,
    IBatteryAlertFeature batteryAlertFeature,
    IClipboardFeature clipboardFeature,
    ISmsFeature smsFeature,
    IFileTransferService fileTransferService,
    IMediaFeature mediaFeature,
    IRemoteMediaFeature remoteMediaFeature,
    IActionFeature actionFeature,
    ISftpFeature sftpFeature,
    ISessionManager sessionManager,
    ICallFeature callFeature,
    IBluetoothPairingService bluetoothPairingService,
    ILogger<MessageHandler> logger) : IMessageHandler
{
    public async void HandleMessageAsync(PairedDevice device, SocketMessage message)
    {
        try
        {
            switch (message)
            {
                case ApplicationList applicationList:
                    await remoteAppRepository.UpdateApplicationList(device, applicationList);
                    break;

                case ApplicationInfo applicationInfo:
                    await remoteAppRepository.AddOrUpdateApplicationForDevice(applicationInfo, device.Id);
                    break;

                case NotificationInfo notificationMessage:
                    await notificationFeature.HandleNotificationMessage(device, notificationMessage);
                    break;

                case MediaAction action:
                    await mediaFeature.HandleMediaActionAsync(action);
                    break;

                case PlaybackInfo playbackSession:
                    await remoteMediaFeature.HandleRemotePlaybackSessionAsync(device, playbackSession);
                    break;

                case BatteryState batteryStatus:
                    await batteryAlertFeature.HandleBatteryStateAsync(device, batteryStatus);
                    break;

                case RingerModeState ringerMode:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.RingerMode = ringerMode.Mode);
                    break;

                case DndState dndStatus:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.DndEnabled = dndStatus.IsEnabled);
                    break;

                case AudioStreamState audioStream:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                        device.UpdateStreamLevel(audioStream.StreamType, audioStream.Level));
                    break;

                case ClipboardInfo clipboard:
                    await clipboardFeature.SetContentAsync(clipboard.Content, device);
                    break;

                case ConversationInfo textConversation:
                    await smsFeature.HandleTextMessage(device.Id, textConversation);
                    break;

                case ContactInfo contactMessage:
                    await contactRepository.SaveContactAsync(device.Id, contactMessage);
                    break;

                case ActionInfo action:
                    actionFeature.HandleActionMessage(action);
                    break;

                case SftpServerInfo sftpServerInfo:
                    await sftpFeature.InitializeAsync(device, sftpServerInfo);
                    break;

                case FileTransferInfo fileTransfer:
                    await fileTransferService.ReceiveFiles(fileTransfer, device);
                    break;

                case DeviceInfo deviceInfo:
                    await deviceManager.UpdateDeviceInfo(device, deviceInfo);
                    break;

                case CallInfo callInfo:
                    await callFeature.HandleCallInfoAsync(device, callInfo);
                    break;

                case CallLogInfo callLogInfo:
                    await callLogRepository.SaveCallLogAsync(device.Id, callLogInfo);
                    break;

                case BluetoothPairingResult pairingResult:
                    bluetoothPairingService.HandleBluetoothPairingResult(device, pairingResult);
                    break;

                case Disconnect:
                    sessionManager.DisconnectDevice(device, true);
                    break;

                default:
                    logger.Warn($"Unknown message type received: {message.GetType().Name}");
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error handling message", ex);
        }
    }
}
