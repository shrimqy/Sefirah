using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Services.Socket;
using Sefirah.Utils.Serialization;

namespace Sefirah.Services;
public class SmsHandlerService(
    ISessionManager sessionManager,
    SmsRepository smsRepository,
    ILogger<SmsHandlerService> logger)
{
    private readonly Dictionary<string, ObservableCollection<Conversation>> deviceConversations = [];
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly DispatcherQueue dispatcher = App.MainWindow.DispatcherQueue;

    public event EventHandler<(string DeviceId, long ThreadId)>? ConversationsUpdated;

    public ObservableCollection<Conversation> GetConversationsForDevice(string deviceId)
    {
        if (!deviceConversations.TryGetValue(deviceId, out ObservableCollection<Conversation>? value))
        {
            value = [];
            deviceConversations[deviceId] = value;
        }
        return value;
    }

    public async Task LoadConversationsFromDatabase(string deviceId)
    {
        try
        {
            logger.LogInformation("Loading conversations from database for device: {DeviceId}", deviceId);
            
            var conversationEntities = await smsRepository.GetConversationsAsync(deviceId);
            var conversations = GetConversationsForDevice(deviceId);
            
            await dispatcher.EnqueueAsync(async () =>
            {
                conversations.Clear();
                
                var conversationList = new List<Conversation>();
                foreach (var entity in conversationEntities)
                {
                    var conversation = await entity.ToConversationAsync(smsRepository);
                    conversations.Add(conversation);
                }
                
                ConversationsUpdated?.Invoke(this, (deviceId, -1)); // -1 indicates all conversations updated
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading conversations from database for device: {DeviceId}", deviceId);
        }
    }

    public async Task HandleTextMessage(string deviceId, TextConversation textConversation)
    {
        await semaphore.WaitAsync();
        try
        {
            var conversations = GetConversationsForDevice(deviceId);
            
            switch (textConversation.ConversationType)
            {
                case ConversationType.Active:
                case ConversationType.New:
                    await HandleActiveOrNewConversation(deviceId, textConversation, conversations);
                    break;
                    
                case ConversationType.ActiveUpdated:
                    await HandleUpdatedConversation(deviceId, textConversation, conversations);
                    break;

                case ConversationType.Removed:
                    await HandleRemovedConversation(deviceId, textConversation, conversations);
                    break;
                    
                default:
                    logger.LogWarning("Unknown conversation type: {ConversationType}", textConversation.ConversationType);
                    break;
            }

            ConversationsUpdated?.Invoke(this, (deviceId, textConversation.ThreadId));
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task HandleActiveOrNewConversation(string deviceId, TextConversation textConversation, ObservableCollection<Conversation> conversations)
    {
        try
        {
            var conversationEntity = await smsRepository.GetConversationAsync(deviceId, textConversation.ThreadId);
            if (conversationEntity is null)
            {
                conversationEntity = SmsRepository.ToEntity(textConversation, deviceId);
            }
            else
            {
                // Update existing conversation entity
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
            var messageEntities = await SaveMessagesFromConversation(deviceId, textConversation);
            
            await dispatcher.EnqueueAsync(async () =>
            {
                var existingConversation = conversations.FirstOrDefault(c => c.ThreadId == textConversation.ThreadId);
                
                if (existingConversation is not null)
                {
                    await existingConversation.UpdateFromTextConversationAsync(textConversation, smsRepository, deviceId);

                    // Move conversation to correct position based on new timestamp
                    SmsHandlerService.InsertOrMoveConversation(existingConversation, conversations);
                }
                else
                {
                    var newConversation = await conversationEntity.ToConversationAsync(smsRepository);
                    SmsHandlerService.InsertOrMoveConversation(newConversation, conversations);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling active/new conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task HandleUpdatedConversation(string deviceId, TextConversation textConversation, ObservableCollection<Conversation> conversations)
    {
        try
        {
            await SaveMessagesFromConversation(deviceId, textConversation);
            var latestMessage = textConversation.Messages.OrderByDescending(m => m.Timestamp).First();

            var conversationEntity = await smsRepository.GetConversationAsync(deviceId, textConversation.ThreadId);
            if (conversationEntity is not null && textConversation.Messages.Count > 0)
            {
                conversationEntity.LastMessageTimestamp = latestMessage.Timestamp;
                conversationEntity.LastMessage = latestMessage.Body;
                if (textConversation.Recipients.Count > 0)
                {
                    conversationEntity.AddressesJson = JsonSerializer.Serialize(textConversation.Recipients);
                }

                await smsRepository.SaveConversationAsync(conversationEntity);
            }
            
            await dispatcher.EnqueueAsync(async () =>
            {
                var existingConversation = conversations.FirstOrDefault(c => c.ThreadId == textConversation.ThreadId);
                if (existingConversation is not null)
                {
                    logger.LogInformation("Adding new messages to conversation: {ThreadId}", existingConversation.ThreadId);
                    existingConversation.LastMessageTimestamp = latestMessage.Timestamp;
                    existingConversation.LastMessage = latestMessage.Body;
                    await existingConversation.NewMessageFromConversationAsync(textConversation, smsRepository, deviceId);

                    // Move conversation to correct position based on new timestamp
                    SmsHandlerService.InsertOrMoveConversation(existingConversation, conversations);
                }
                else
                {
                    logger.LogInformation("Updated conversation not found in UI, creating new: {ThreadId}", textConversation.ThreadId);
                    var conversationEntity = SmsRepository.ToEntity(textConversation, deviceId);
                    var newConversation = await conversationEntity.ToConversationAsync(smsRepository);
                    SmsHandlerService.InsertOrMoveConversation(newConversation, conversations);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling updated conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task HandleRemovedConversation(string deviceId, TextConversation textConversation, ObservableCollection<Conversation> conversations)
    {
        try
        {
            await smsRepository.DeleteConversationAsync(deviceId, textConversation.ThreadId);
            await dispatcher.EnqueueAsync(() =>
            {
                var existingConversation = conversations.FirstOrDefault(c => c.ThreadId == textConversation.ThreadId);
                if (existingConversation is not null)
                {
                    conversations.Remove(existingConversation);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling removed conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task<List<MessageEntity>?> SaveMessagesFromConversation(string deviceId, TextConversation textConversation)
    {
        try
        {
            var messageEntities = textConversation.Messages
                .Select(m => SmsRepository.ToEntity(m, deviceId))
                .ToList();
            
            await smsRepository.SaveMessagesAsync(messageEntities);

            // TODO: Handle attachments 

            return messageEntities;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving messages for device {DeviceId}", deviceId);
            return null;
        }
    }


    public async Task<List<Message>> LoadMessagesForConversation(string deviceId, long threadId)
    {
        try
        {
            var messageEntities = await smsRepository.GetMessagesWithAttachmentsAsync(deviceId, threadId);
            var messages = new List<Message>();
            
            foreach (var entity in messageEntities)
            {
                var message = await entity.ToMessageAsync(smsRepository);
                messages.Add(message);
            }
            
            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading messages for thread {ThreadId}, device {DeviceId}", threadId, deviceId);
            return [];
        }
    }

    public void RequestThreadHistory(ServerSession session, long threadId, long rangeStartTimestamp = -1, long numberToRequest = -1)
    {
        var threadRequest = new ThreadRequest
        {
            ThreadId = threadId,
            RangeStartTimestamp = rangeStartTimestamp,
            NumberToRequest = numberToRequest
        };
        sessionManager.SendMessage(session, SocketMessageSerializer.Serialize(threadRequest));
    }

    public void SendTextMessage(ServerSession session, TextMessage textMessage)
    {
        sessionManager.SendMessage(session, SocketMessageSerializer.Serialize(textMessage));
    }

    public async Task HandleContactMessage(string deviceId, ContactMessage contactMessage)
    {
        try
        {
            var contactEntity = SmsRepository.ToEntity(contactMessage, deviceId);
            await smsRepository.SaveContactAsync(contactEntity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling contact message {ContactId} for device {DeviceId}", 
                contactMessage.Id, deviceId);
        }
    }

    public void ClearConversationsForDevice(string deviceId)
    {
        if (deviceConversations.TryGetValue(deviceId, out ObservableCollection<Conversation>? value))
        {
            value.Clear();
        }
    }

    public static void InsertOrMoveConversation(Conversation conversation, ObservableCollection<Conversation> conversations)
    {
        var existingIndex = conversations.IndexOf(conversation);
        var targetIndex = FindInsertionIndex(conversations, conversation.LastMessageTimestamp);


        if (existingIndex >= 0)
        {
            if (existingIndex == targetIndex || (existingIndex == targetIndex - 1))
            {
                return;
            }

            conversations.RemoveAt(existingIndex);
            if (existingIndex < targetIndex)
            {
                targetIndex--;
            }
        }
        conversations.Insert(targetIndex, conversation);
    }

    private static int FindInsertionIndex(ObservableCollection<Conversation> conversations, long timestamp)
    {
        for (int i = 0; i < conversations.Count; i++)
        {
            if (conversations[i].LastMessageTimestamp <= timestamp)
            {
                return i;
            }
        }
        return conversations.Count;
    }

    public async Task<ObservableCollection<Contact>> GetAllContactsAsync()
    {   
        var contacts = await smsRepository.GetAllContactsAsync();
        var senders = await Task.WhenAll(contacts.Select(c => c.ToContact()));
        return senders.ToObservableCollection();
    }
}
