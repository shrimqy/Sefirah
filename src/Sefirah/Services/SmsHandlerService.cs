using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Services.Socket;
using Sefirah.Utils.Serialization;
using Uno.Extensions;
using Uno.Logging;

namespace Sefirah.Services;
public class SmsHandlerService(
    ISessionManager sessionManager,
    SmsRepository smsRepository,
    ILogger<SmsHandlerService> logger)
{
    // Device-specific conversations for UI binding
    private readonly Dictionary<string, ObservableCollection<SmsConversation>> deviceConversations = new();
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly DispatcherQueue dispatcher = App.MainWindow!.DispatcherQueue;

    // Event to notify when conversations are updated for a device
    public event EventHandler<string>? ConversationsUpdated;

    public ObservableCollection<SmsConversation> GetConversationsForDevice(string deviceId)
    {
        if (!deviceConversations.ContainsKey(deviceId))
        {
            deviceConversations[deviceId] = [];
        }
        return deviceConversations[deviceId];
    }

    public async Task LoadConversationsFromDatabase(string deviceId)
    {
        try
        {
            logger.LogInformation("Loading conversations from database for device: {DeviceId}", deviceId);
            
            var conversationEntities = await smsRepository.GetConversationsAsync(deviceId);
            var conversations = GetConversationsForDevice(deviceId);
            
            await dispatcher.EnqueueAsync(() =>
            {
                conversations.Clear();
                
                foreach (var entity in conversationEntities)
                {
                    // Create SmsConversation from database entity
                    var conversation = new SmsConversation
                    {
                        ThreadId = entity.ThreadId,
                        LastMessage = entity.LastMessageBody ?? string.Empty,
                        LastMessageTimestamp = entity.LastMessageTimestamp,
                        DisplayName = entity.DisplayName
                    };
                    
                    conversations.Add(conversation);
                }

                // Fire event to notify listeners
                ConversationsUpdated?.Invoke(this, deviceId);
            });
            
            logger.LogInformation("Loaded {Count} conversations for device: {DeviceId}", conversationEntities.Count, deviceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading conversations from database for device: {DeviceId}", deviceId);
        }
    }

    public async Task HandleTextMessage(string deviceId, TextConversation textConversation)
    {
        logger.LogInformation("Received text conversation: {ThreadId} with type: {ConversationType} from device: {DeviceId}", 
            textConversation.ThreadId, textConversation.ConversationType, deviceId);

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

            // Fire event to notify listeners that conversations were updated
            ConversationsUpdated?.Invoke(this, deviceId);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task HandleActiveOrNewConversation(string deviceId, TextConversation textConversation, ObservableCollection<SmsConversation> conversations)
    {
        try
        {
            // Save/update conversation in database
            var conversationEntity = await smsRepository.GetConversationAsync(deviceId, textConversation.ThreadId);
            var isNewConversation = conversationEntity == null;
            
            // Save contacts
            await SaveContactsFromConversation(deviceId, textConversation);
            
            // Save messages
            await SaveMessagesFromConversation(deviceId, textConversation);
            
            // Create or update conversation entity
            var smsConversation = isNewConversation ? new SmsConversation(textConversation) : null;
            
            if (smsConversation != null)
            {
                conversationEntity = SmsRepository.ToEntity(smsConversation, deviceId);
            }
            else
            {
                // Update existing entity
                if (textConversation.Messages.Count > 0)
                {
                    var latestMessage = textConversation.Messages.OrderByDescending(m => m.Timestamp).First();
                    conversationEntity!.LastMessageTimestamp = latestMessage.Timestamp;
                    conversationEntity.LastMessageBody = latestMessage.Body;
                    
                    var displayName = textConversation.Messages.FirstOrDefault()?.Contacts.FirstOrDefault()?.ContactName ?? string.Empty;
                    if (string.IsNullOrEmpty(displayName))
                    {
                        displayName = textConversation.Messages.FirstOrDefault()?.Addresses.FirstOrDefault() ?? string.Empty;
                    }
                    conversationEntity.DisplayName = displayName;
                }
            }
            
            await smsRepository.SaveConversationAsync(conversationEntity!);
            
            // Update UI
            await dispatcher.EnqueueAsync(() =>
            {
                var existingConversation = conversations.FirstOrDefault(c => c.ThreadId == textConversation.ThreadId);
                
                if (existingConversation != null)
                {
                    logger.LogInformation("Updating existing conversation: {ThreadId}", existingConversation.ThreadId);
                    existingConversation.UpdateFromTextConversation(textConversation);
                }
                else
                {
                    logger.LogInformation("Adding new conversation: {ThreadId}", textConversation.ThreadId);
                    if (smsConversation != null)
                    {
                        AddNewConversation(smsConversation, conversations);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling active/new conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task HandleUpdatedConversation(string deviceId, TextConversation textConversation, ObservableCollection<SmsConversation> conversations)
    {
        try
        {
            // Save new contacts
            await SaveContactsFromConversation(deviceId, textConversation);
            
            // Save new messages
            await SaveMessagesFromConversation(deviceId, textConversation);
            
            // Update conversation entity
            var conversationEntity = await smsRepository.GetConversationAsync(deviceId, textConversation.ThreadId);
            if (conversationEntity != null && textConversation.Messages.Count > 0)
            {
                var latestMessage = textConversation.Messages.OrderByDescending(m => m.Timestamp).First();
                conversationEntity.LastMessageTimestamp = latestMessage.Timestamp;
                conversationEntity.LastMessageBody = latestMessage.Body;
                await smsRepository.SaveConversationAsync(conversationEntity);
            }
            
            // Update UI
            await dispatcher.EnqueueAsync(() =>
            {
                var existingConversation = conversations.FirstOrDefault(c => c.ThreadId == textConversation.ThreadId);
                if (existingConversation != null)
                {
                    logger.LogInformation("Adding new messages to conversation: {ThreadId}", existingConversation.ThreadId);
                    existingConversation.NewMessageFromConversation(textConversation);
                }
                else
                {
                    logger.LogInformation("Updated conversation not found in UI, creating new: {ThreadId}", textConversation.ThreadId);
                    var newConversation = new SmsConversation(textConversation);
                    AddNewConversation(newConversation, conversations);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling updated conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task HandleRemovedConversation(string deviceId, TextConversation textConversation, ObservableCollection<SmsConversation> conversations)
    {
        try
        {
            // Remove from database
            await smsRepository.DeleteConversationAsync(deviceId, textConversation.ThreadId);
            
            // Remove from UI
            await dispatcher.EnqueueAsync(() =>
            {
                var existingConversation = conversations.FirstOrDefault(c => c.ThreadId == textConversation.ThreadId);
                if (existingConversation != null)
                {
                    logger.LogInformation("Removing conversation: {ThreadId}", existingConversation.ThreadId);
                    conversations.Remove(existingConversation);
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling removed conversation {ThreadId} for device {DeviceId}", textConversation.ThreadId, deviceId);
        }
    }

    private async Task SaveContactsFromConversation(string deviceId, TextConversation textConversation)
    {
        try
        {
            var contactsToSave = new List<Contact>();
            
            foreach (var message in textConversation.Messages)
            {
                contactsToSave.AddRange(message.Contacts);
            }
            
            if (contactsToSave.Count > 0)
            {
                var contactEntities = contactsToSave
                    .DistinctBy(c => c.PhoneNumber)
                    .Select(c => SmsRepository.ToEntity(c, deviceId))
                    .ToList();
                
                await smsRepository.SaveContactsAsync(contactEntities);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving contacts for device {DeviceId}", deviceId);
        }
    }

    private async Task SaveMessagesFromConversation(string deviceId, TextConversation textConversation)
    {
        try
        {
            var messageEntities = textConversation.Messages
                .Select(m => SmsRepository.ToEntity(m, deviceId))
                .ToList();
            
            await smsRepository.SaveMessagesAsync(messageEntities);
            
            // Save attachments
            //var attachmentsToSave = new List<SmsAttachmentEntity>();
            //foreach (var message in textConversation.Messages)
            //{
            //    if (message.Attachments != null && message.Attachments.Count > 0)
            //    {
            //        var messageAttachments = message.Attachments
            //            .Select(a => SmsRepository.ToEntity(a, message.UniqueId, deviceId))
            //            .ToList();
            //        attachmentsToSave.AddRange(messageAttachments);
            //    }
            //}
            
            //if (attachmentsToSave.Count > 0)
            //{
            //    await smsRepository.SaveAttachmentsAsync(attachmentsToSave);
            //}
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving messages for device {DeviceId}", deviceId);
        }
    }

    public void AddNewConversation(SmsConversation conversation, ObservableCollection<SmsConversation> conversations)
    {
        int index = FindInsertionIndex(conversation, conversations);
        conversations.Insert(index, conversation);
    }

    private static int FindInsertionIndex(SmsConversation conversation, ObservableCollection<SmsConversation> conversations)
    {
        for (int i = 0; i < conversations.Count; i++)
        {
            // Compare timestamps - if current conversation is older than the new one, insert here
            if (conversations[i].LastMessageTimestamp < conversation.LastMessageTimestamp)
            {
                return i;
            }
        }

        // If we get here, this is the oldest conversation or collection is empty
        return conversations.Count;
    }

    public async Task<List<TextMessage>> LoadMessagesForConversation(string deviceId, long threadId)
    {
        try
        {
            var messageEntities = await smsRepository.GetMessagesWithAttachmentsAsync(deviceId, threadId);
            return messageEntities.Select(SmsRepository.FromEntity).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading messages for thread {ThreadId}, device {DeviceId}", threadId, deviceId);
            return [];
        }
    }

    public async Task RequestThreadHistory(ServerSession session, long threadId, long rangeStartTimestamp = -1, long numberToRequest = -1)
    {
        var threadRequest = new ThreadRequest
        {
            ThreadId = threadId,
            RangeStartTimestamp = rangeStartTimestamp,
            NumberToRequest = numberToRequest
        };
        sessionManager.SendMessage(session, SocketMessageSerializer.Serialize(threadRequest));
    }

    public async Task SendTextMessage(ServerSession session, TextMessage textMessage)
    {
        logger.LogInformation("Sending text message: {Body} to {Address}", textMessage.Body, textMessage.Addresses.FirstOrDefault());
        sessionManager.SendMessage(session, SocketMessageSerializer.Serialize(textMessage));
    }

    public void ClearConversationsForDevice(string deviceId)
    {
        if (deviceConversations.ContainsKey(deviceId))
        {
            deviceConversations[deviceId].Clear();
        }
    }
}
