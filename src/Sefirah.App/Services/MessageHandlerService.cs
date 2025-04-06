using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Services;

public class MessageHandlerService(
    ILogger logger,
    INotificationService notificationService,
    ISmsHandlerService smsHandlerService,
    IClipboardService clipboardService,
    IPlaybackService playbackService,
    ISftpService sftpService,
    IDeviceManager deviceManager,
    IRemoteAppsRepository remoteAppsRepository,
    IFileTransferService fileTransferService,
    ICommandService commandService) : IMessageHandlerService
{
    public Task HandleBinaryData(byte[] data)
    {
        throw new NotImplementedException();
    }

    public async Task HandleJsonMessage(SocketMessage message)
    {
        try
        {
            
            switch (message)
            {
                case CommandMessage commandMessage:
                    commandService.HandleCommand(commandMessage);
                    break;
                case DeviceStatus deviceStatus:
                    await deviceManager.UpdateDeviceStatus(deviceStatus);
                    break;

                case ClipboardMessage clipboardMessage:
                    await clipboardService.SetContentAsync(clipboardMessage.Content);
                    break;

                case NotificationMessage notificationMessage:
                    await notificationService.HandleNotificationMessage(notificationMessage);
                    break;

                case PlaybackAction action:
                   await playbackService.HandleMediaActionAsync(action);
                   break;

                case ApplicationInfo applicationInfo:
                    await remoteAppsRepository.AddOrUpdateAsync(ApplicationInfoEntity.FromApplicationInfo(applicationInfo));
                    break;

                case SftpServerInfo sftpServerInfo:
                    await sftpService.InitializeAsync(sftpServerInfo);
                    break;

                case FileTransfer fileTransfer:
                    await fileTransferService.ReceiveFile(fileTransfer);
                    break;

                case TextConversation textConversation:
                    await smsHandlerService.HandleTextMessage(textConversation);
                    break;
                    

                default:
                    logger.Warn("Unknown message type received: {0}", message.GetType().Name);
                    break;
            }
        }
        catch (InvalidCastException ex)
        {
            logger.Error($"Invalid message type cast. Expected type: {message.GetType().Name}, Actual type: {ex.GetType().Name}", ex);
            throw;
        }
        catch (Exception ex)
        {
            logger.Error("Error handling message", ex);
            throw;
        }
    }
}
