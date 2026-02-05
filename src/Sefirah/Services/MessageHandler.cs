using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;

namespace Sefirah.Services;
public class MessageHandler(
    RemoteAppRepository remoteAppRepository,
    IDeviceManager deviceManager,
    INotificationService notificationService,
    IClipboardService clipboardService,
    SmsHandlerService smsHandlerService,
    IFileTransferService fileTransferService,
    IMediaService playbackService,
    IRemoteMediaHandler remotePlaybackService,
    IActionService actionService,
    ISftpService sftpService,
    ISessionManager sessionManager,
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
                    await notificationService.HandleNotificationMessage(device, notificationMessage);
                    break;

                case MediaAction action:
                    await playbackService.HandleMediaActionAsync(action);
                    break;

                case PlaybackInfo playbackSession:
                    await remotePlaybackService.HandleRemotePlaybackSessionAsync(device, playbackSession);
                    break;

                case BatteryState batteryStatus:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BatteryStatus = batteryStatus);
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
                    await clipboardService.SetContentAsync(clipboard.Content, device);
                    break;

                case ConversationInfo textConversation:
                    await smsHandlerService.HandleTextMessage(device.Id, textConversation);
                    break;

                case ContactInfo contactMessage:
                    await smsHandlerService.HandleContactMessage(device.Id, contactMessage);
                    break;

                case ActionInfo action:
                    actionService.HandleActionMessage(action);
                    break;

                case SftpServerInfo sftpServerInfo:
                    await sftpService.InitializeAsync(device, sftpServerInfo);
                    break;

                case FileTransferInfo fileTransfer:
                    await fileTransferService.ReceiveFiles(fileTransfer, device);
                    break;

                case DeviceInfo deviceInfo:
                    await deviceManager.UpdateDeviceInfo(device, deviceInfo);
                    break;

                case Disconnect:
                    sessionManager.DisconnectDevice(device, true);
                    break;

                default:
                    logger.LogWarning("Unknown message type received: {type}", message.GetType().Name);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling message");
        }
    }
}
