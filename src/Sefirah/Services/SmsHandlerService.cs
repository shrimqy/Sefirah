using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Services;
public class SmsHandlerService(
    SmsRepository smsRepository,
    ContactRepository contactRepository,
    ILogger logger)
{
    private readonly SemaphoreSlim semaphore = new(1, 1);

    public event EventHandler<(string DeviceId, long ThreadId)>? ConversationRemoved;
    public event EventHandler<(string DeviceId, long ThreadId, Conversation Conversation, IReadOnlyList<Message> NewMessages)>? ConversationUpdated;

    /// <summary>
    /// Loads conversation summaries for a device from the database. Does not load messages.
    /// </summary>
    public async Task<List<Conversation>> LoadConversationAsync(string deviceId)
    {
        try
        {
            var conversationEntities = await smsRepository.GetConversationsAsync(deviceId);
            var list = new List<Conversation>();
            foreach (var entity in conversationEntities)
            {
                var conversation = await entity.ToConversationAsync(contactRepository);
                list.Add(conversation);
            }
            return list;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading conversations from database for device: {DeviceId}", deviceId);
            return [];
        }
    }

    public async Task HandleTextMessage(string deviceId, ConversationInfo textConversation)
    {
        await semaphore.WaitAsync();
        try
        {
            switch (textConversation.InfoType)
            {
                // refactor later
                case ConversationInfoType.Active:
                case ConversationInfoType.New:
                case ConversationInfoType.ActiveUpdated:
                    await HandleConversationUpdate(deviceId, textConversation);
                    break;
                case ConversationInfoType.Removed:
                    await HandleRemovedConversation(deviceId, textConversation);
                    break;
                default:
                    logger.LogWarning("Unknown conversation type: {ConversationType}", textConversation.InfoType);
                    break;
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<List<Message>> ToMessagesAsync(string deviceId, List<TextMessage> textMessages)
    {
        var messages = new List<Message>();
        foreach (var tm in textMessages)
        {
            var address = tm.Addresses.Count > 0 ? tm.Addresses[0] : string.Empty;
            var contactEntity = await contactRepository.GetContactAsync(deviceId, address);
            var participant = contactEntity is not null
                ? await App.MainWindow.DispatcherQueue.EnqueueAsync(() => contactEntity.ToParticipantInfo())
                : new ParticipantInfo(address, address);
            messages.Add(new Message
            {
                UniqueId = tm.UniqueId,
                ThreadId = tm.ThreadId,
                Body = tm.Body,
                Timestamp = tm.Timestamp,
                MessageType = tm.MessageType,
                Read = tm.Read,
                SubscriptionId = tm.SubscriptionId,
                Attachments = tm.Attachments,
                Participant = participant,
                IsTextMessage = tm.IsTextMessage
            });
        }
        return messages;
    }

    private async Task HandleConversationUpdate(string deviceId, ConversationInfo textConversation)
    {
        try
        {
            var conversationEntity = await smsRepository.GetConversationAsync(deviceId, textConversation.ThreadId);
            if (conversationEntity is null)
            {
                conversationEntity = textConversation.ToEntity(deviceId);
            }
            else
            {
                if (textConversation.Messages.Count > 0)
                {
                    var latestMessage = textConversation.Messages.OrderByDescending(m => m.Timestamp).First();
                    conversationEntity.LastMessageTimestamp = latestMessage.Timestamp;
                    conversationEntity.LastMessage = latestMessage.Body;
                }
                if (textConversation.Recipients.Count > 0)
                {
                    conversationEntity.AddressesJson = JsonSerializer.Serialize(textConversation.Recipients);
                }
                conversationEntity.HasRead = textConversation.Messages.Any(m => m.Read);
            }

            await smsRepository.SaveConversationAsync(conversationEntity);
            await SaveMessagesFromConversation(deviceId, textConversation);
            var conversation = await conversationEntity.ToConversationAsync(contactRepository);
            var newMessages = await ToMessagesAsync(deviceId, textConversation.Messages);
            ConversationUpdated?.Invoke(this, (deviceId, textConversation.ThreadId, conversation, newMessages));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling conversation update {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task HandleRemovedConversation(string deviceId, ConversationInfo textConversation)
    {
        try
        {
            await smsRepository.DeleteConversationAsync(deviceId, textConversation.ThreadId);
            ConversationRemoved?.Invoke(this, (deviceId, textConversation.ThreadId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling removed conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task SaveMessagesFromConversation(string deviceId, ConversationInfo textConversation)
    {
        try
        {
            var messageEntities = textConversation.Messages
                .Select(m => m.ToEntity(deviceId))
                .ToList();
            await smsRepository.SaveMessagesAsync(messageEntities);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving messages for device {DeviceId}", deviceId);
        }
    }


    public async Task<List<Message>> LoadMessagesForConversation(string deviceId, long threadId)
    {
        var messageEntities = await smsRepository.GetMessagesWithAttachmentsAsync(deviceId, threadId);
        List<Message> messages = [];
            
        foreach (var entity in messageEntities)
        {
            var message = await entity.ToMessageAsync(contactRepository);
            messages.Add(message);
        }
        return messages;
    }

    public static async Task RequestThreadHistory(PairedDevice device, long threadId, long rangeStartTimestamp = -1, long numberToRequest = -1)
    {
        var threadRequest = new ThreadRequest
        {
            ThreadId = threadId,
            RangeStartTimestamp = rangeStartTimestamp,
            NumberToRequest = numberToRequest
        };

        device.SendMessage(threadRequest);
    }
}
