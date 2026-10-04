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
    IAudioFeature audioFeature,
    IRemoteMediaFeature remoteMediaFeature,
    IActionFeature actionFeature,
    ISftpFeature sftpFeature,
    ISessionManager sessionManager,
    ICallFeature callFeature,
    IBluetoothPairingService bluetoothPairingService,
    IPlaySoundFeature playSoundFeature,
    IAdbService adbService,
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

                case AudioAction action:
                    await audioFeature.HandleAudioActionAsync(action);
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

                case BluetoothState bluetoothState:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BluetoothEnabled = bluetoothState.IsEnabled);
                    break;

                case AudioStreamState audioStream:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                        device.UpdateStreamLevel(audioStream.StreamType, audioStream.Level));
                    break;

                case ClipboardInfo clipboard:
                    await clipboardFeature.SetContentAsync(clipboard, device);
                    break;

                case ConversationInfo conversationInfo:
                    await smsFeature.HandleConversationInfo(device.Id, conversationInfo);
                    break;

                case MessageList messageList:
                    await smsFeature.HandleMessageList(device.Id, messageList);
                    break;

                case MessageIndex messageIndex:
                    await smsFeature.HandleMessageIndex(device.Id, messageIndex);
                    break;

                case RemoveConversation removeConversation:
                    await smsFeature.HandleRemoveConversation(device.Id, removeConversation);
                    break;

                case ContactInfo contactMessage:
                    await contactRepository.SaveContactAsync(device.Id, contactMessage);
                    break;

                case ActionInfo action:
                    await actionFeature.HandleActionMessage(action);
                    break;

                case SftpServerInfo sftpServerInfo:
                    await sftpFeature.Mount(device, sftpServerInfo);
                    break;

                case ShareTransfer share:
                    var sharedFile = await fileTransferService.Receive(share.Transfer, device, silent: false);
                    if (sharedFile is not null && device.DeviceSettings.ClipboardFiles)
                        await clipboardFeature.SetContentAsync(sharedFile, device);
                    break;

                case ClipboardTransfer clipboardTransfer:
                    var clipboardFile = await fileTransferService.Receive(clipboardTransfer.Transfer, device, silent: true);
                    if (clipboardFile is not null)
                        await clipboardFeature.SetContentAsync(clipboardFile, device);
                    break;

                case SmsAttachmentTransfer smsAttachmentTransfer:
                    StorageFile? attachmentFile = null;
                    try
                    {
                        attachmentFile = await fileTransferService.Receive(smsAttachmentTransfer.Transfer, device, silent: true);
                    }
                    finally
                    {
                        await smsFeature.HandleAttachmentFile(device.Id, smsAttachmentTransfer.PartId, attachmentFile);
                    }
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

                case PlaySound playSound:
                    await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
                        playSoundFeature.HandleRemoteState(device, playSound.IsPlaying));
                    break;

                case RequestWorkerLaunch requestWorkerLaunch:
                    await adbService.TryStartWorkerAsync(device, requestWorkerLaunch.Command);
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
