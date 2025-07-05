using Sefirah.Data.AppDatabase.Models;
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
    IPlaybackService playbackService,
    IActionService actionService,
    ISftpService sftpService,
    ILogger<MessageHandler> logger) : IMessageHandler
{
    public async Task HandleMessageAsync(PairedDevice device, SocketMessage message)
    {
        try
        {
            switch (message)
            {
                case ApplicationInfo applicationInfo:
                    remoteAppRepository.AddOrUpdateApplication(ApplicationInfoEntity.FromApplicationInfo(applicationInfo, device.Id));
                    break;

                case ApplicationList applicationList:
                    remoteAppRepository.UpdateApplicationList(device, applicationList);
                    break;

                case NotificationMessage notificationMessage:
                    await notificationService.HandleNotificationMessage(device, notificationMessage);
                    break;

                case PlaybackAction action:
                    await playbackService.HandleMediaActionAsync(action);
                    break;

                case DeviceStatus deviceStatus:
                    deviceManager.UpdateDeviceStatus(device, deviceStatus);
                    break;

                case ClipboardMessage clipboardMessage:
                    await clipboardService.SetContentAsync(clipboardMessage.Content, device);
                    break;

                case TextConversation textConversation:
                    await smsHandlerService.HandleTextMessage(device.Id, textConversation);
                    break;

                case ActionMessage action:
                    actionService.HandleActionMessage(action);
                    break;

                case SftpServerInfo sftpServerInfo:
                    await sftpService.InitializeAsync(device, sftpServerInfo);
                    break;

                case FileTransfer fileTransfer:
                    await fileTransferService.ReceiveFile(fileTransfer, device);
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
