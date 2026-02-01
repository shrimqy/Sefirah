using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;

namespace Sefirah.Services;
public class MessageHandler(
    RemoteAppRepository remoteAppRepository,
    IDeviceManager deviceManager,
    INotificationService notificationService,
    IClipboardService clipboardService,
    SmsHandlerService smsHandlerService,
    IFileTransferService fileTransferService,
    IPlaybackService playbackService,
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

                case NotificationMessage notificationMessage:
                    await notificationService.HandleNotificationMessage(device, notificationMessage);
                    break;

                case PlaybackAction action:
                    await playbackService.HandleMediaActionAsync(action);
                    break;

                case BatteryStatus batteryStatus:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BatteryStatus = batteryStatus);
                    break;

                case RingerMode ringerMode:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.RingerMode = ringerMode.Mode);
                    break;

                case DndStatus dndStatus:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.DndEnabled = dndStatus.IsEnabled);
                    break;

                case AudioStreamMessage audioStream:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => 
                        device.Audio.Update(audioStream.StreamType, audioStream.Level, audioStream.MaxLevel));
                    break;

                case ClipboardMessage clipboardMessage:
                    await clipboardService.SetContentAsync(clipboardMessage.Content, device);
                    break;

                case ConversationMessage textConversation:
                    await smsHandlerService.HandleTextMessage(device.Id, textConversation);
                    break;

                case ContactMessage contactMessage:
                    await smsHandlerService.HandleContactMessage(device.Id, contactMessage);
                    break;

                case ActionMessage action:
                    actionService.HandleActionMessage(action);
                    break;

                case SftpServerInfo sftpServerInfo:
                    if (device.DeviceSettings.StorageAccess)
                    {
                        await sftpService.InitializeAsync(device, sftpServerInfo);
                    }
                    break;
                case FileTransferMessage fileTransfer:
                    await fileTransferService.ReceiveFiles(fileTransfer, device);
                    break;
                case DeviceInfo deviceInfo:
                    await deviceManager.UpdateDeviceInfo(device, deviceInfo);
                    break;
                case CommandMessage commandMessage:
                    if (commandMessage.CommandType is CommandType.Disconnect)
                    {
                        sessionManager.DisconnectDevice(device, true);
                    }
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
