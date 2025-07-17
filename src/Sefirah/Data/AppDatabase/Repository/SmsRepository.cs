using System.Runtime.CompilerServices;
using System.Text.Json;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using SQLite;

namespace Sefirah.Data.AppDatabase.Repository;

public class SmsRepository
{
    private readonly DatabaseContext databaseContext;
    private readonly ILogger<SmsRepository> logger;

    public SmsRepository(DatabaseContext databaseContext, ILogger<SmsRepository> logger)
    {
        this.databaseContext = databaseContext;
        this.logger = logger;
    }

    #region Conversation Operations

    public async Task<ConversationEntity?> GetConversationAsync(string deviceId, long threadId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<ConversationEntity>()
                    .FirstOrDefault(c => c.DeviceId == deviceId && c.ThreadId == threadId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting conversation for device {DeviceId}, thread {ThreadId}", deviceId, threadId);
            return null;
        }
    }

    public async Task<List<ConversationEntity>> GetConversationsAsync(string deviceId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<ConversationEntity>()
                    .Where(c => c.DeviceId == deviceId)
                    .OrderByDescending(c => c.LastMessageTimestamp)
                    .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting conversations for device {DeviceId}", deviceId);
            return [];
        }
    }

    public async Task<bool> SaveConversationAsync(ConversationEntity conversation)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertOrReplace(conversation));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving conversation {ThreadId} for device {DeviceId}", conversation.ThreadId, conversation.DeviceId);
            return false;
        }
    }

    public async Task<bool> DeleteConversationAsync(string deviceId, long threadId)
    {
        try
        {
            await Task.Run(() =>
            {
                // Delete conversation
                databaseContext.Database.Delete<ConversationEntity>(threadId);
                
                // Delete associated messages
                databaseContext.Database.Execute("DELETE FROM TextMessageEntity WHERE DeviceId = ? AND ThreadId = ?", deviceId, threadId);
                
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting conversation {ThreadId} for device {DeviceId}", threadId, deviceId);
            return false;
        }
    }

    #endregion

    #region Message Operations

    public async Task<List<MessageEntity>> GetMessagesAsync(string deviceId, long threadId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<MessageEntity>()
                    .Where(m => m.DeviceId == deviceId && m.ThreadId == threadId)
                    .OrderBy(m => m.Timestamp)
                    .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting messages for device {DeviceId}, thread {ThreadId}", deviceId, threadId);
            return [];
        }
    }

    public async Task<List<MessageEntity>> GetMessagesWithAttachmentsAsync(string deviceId, long threadId)
    {
        try
        {
            var messages = await GetMessagesAsync(deviceId, threadId);
            
            //// Load attachments for each message
            //foreach (var message in messages)
            //{
            //    var attachments = await GetAttachmentsAsync(message.UniqueId);
            //    // Convert attachments to TextMessage.Attachments format if needed
            //    if (attachments.Count > 0)
            //    {
            //        //message.Attachments = attachments.Select(a => new SmsAttachment
            //        //{
            //        //    Base64Data = a.Data
            //        //}).ToList();
            //    }
            //}
            
            return messages;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting messages with attachments for device {DeviceId}, thread {ThreadId}", deviceId, threadId);
            return [];
        }
    }

    public List<MessageEntity>? GetMessageAsync(string deviceId, long uniqueId)
    {
        try
        {
            return databaseContext.Database.Table<MessageEntity>().Where(m => m.DeviceId == deviceId && m.UniqueId == uniqueId).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting message {UniqueId} for device {DeviceId}", uniqueId, deviceId);
            return null;
        }
    }

    public async Task<bool> SaveMessageAsync(MessageEntity message)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertOrReplace(message));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving message {UniqueId} for device {DeviceId}", message.UniqueId, message.DeviceId);
            return false;
        }
    }

    public async Task<bool> SaveMessagesAsync(List<MessageEntity> messages)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertAll(messages, "OR REPLACE"));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving batch messages");
            return false;
        }
    }

    public async Task<bool> DeleteMessageAsync(string deviceId, long uniqueId)
    {
        try
        {
            await Task.Run(() =>
            {
                databaseContext.Database.Execute("DELETE FROM TextMessageEntity WHERE DeviceId = ? AND UniqueId = ?", deviceId, uniqueId);
                // Note: Attachment deletion removed for now as per user request
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting message {UniqueId} for device {DeviceId}", uniqueId, deviceId);
            return false;
        }
    }

    #endregion

    #region Contact Operations

    public async Task<List<ContactEntity>> GetContactsAsync(string deviceId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<ContactEntity>()
                    .Where(c => c.DeviceId == deviceId)
                    .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contacts for device {DeviceId}", deviceId);
            return [];
        }
    }

    public async Task<List<ContactEntity>> GetAllContactsAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                return databaseContext.Database.Table<ContactEntity>().ToList();
            });
        }
        catch
        {
            return [];
        }
    }


    public async Task<ContactEntity?> GetContactAsync(string deviceId, string phoneNumber)  
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<ContactEntity>()
                    .FirstOrDefault(c => c.DeviceId == deviceId && c.Number == phoneNumber));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contact {PhoneNumber} for device {DeviceId}", phoneNumber, deviceId);
            return null;
        }
    }

    public async Task<ContactEntity?> GetContactByIdAsync(string deviceId, string contactId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<ContactEntity>()
                    .FirstOrDefault(c => c.DeviceId == deviceId && c.Id == contactId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contact by ID {ContactId} for device {DeviceId}", contactId, deviceId);
            return null;
        }
    }

    public async Task<bool> SaveContactAsync(ContactEntity contact)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertOrReplace(contact));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving contact {PhoneNumber} for device {DeviceId}", contact.Number, contact.DeviceId);
            return false;
        }
    }

    public async Task<bool> SaveContactsAsync(List<ContactEntity> contacts)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertAll(contacts, "OR REPLACE"));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving batch contacts");
            return false;
        }
    }

    #endregion

    #region Attachment Operations

    public async Task<List<AttachmentEntity>> GetAttachmentsAsync(long messageUniqueId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<AttachmentEntity>()
                    .Where(a => a.MessageUniqueId == messageUniqueId)
                    .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError("Error getting attachments for message {MessageUniqueId}, device {DeviceId}", messageUniqueId, ex);
            return [];
        }
    }

    public async Task<bool> SaveAttachmentAsync(AttachmentEntity attachment)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertOrReplace(attachment));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError("Error saving attachment for message {MessageUniqueId}, device {DeviceId}",attachment.MessageUniqueId, ex);
            return false;
        }
    }

    public async Task<bool> SaveAttachmentsAsync(List<AttachmentEntity> attachments)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertAll(attachments, "OR REPLACE"));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving batch attachments");
            return false;
        }
    }

    #endregion

    #region Helper Methods

    public static MessageEntity ToEntity(TextMessage message, string deviceId)
    {
        return new MessageEntity
        {
            UniqueId = message.UniqueId,
            ThreadId = message.ThreadId ?? 0,
            DeviceId = deviceId,
            Body = message.Body,
            Timestamp = message.Timestamp,
            Read = message.Read,
            SubscriptionId = message.SubscriptionId,
            MessageType = message.MessageType,
            Address = message.Addresses[0] // 0 index is for sender
        };
    }

    public static ContactEntity ToEntity(ContactMessage contact, string deviceId)
    {
        byte[]? avatar = null;
        if (!string.IsNullOrEmpty(contact.PhotoBase64))
        {
            try
            {
                avatar = Convert.FromBase64String(contact.PhotoBase64);
            }
            catch (Exception)
            {
                avatar = null;
            }
        }

        return new ContactEntity
        {
            Id = contact.Id,
            DeviceId = deviceId,
            LookupKey = contact.LookupKey,
            DisplayName = contact.DisplayName,
            Number = contact.Number,
            Avatar = avatar
        };
    }

    public static AttachmentEntity ToEntity(SmsAttachment attachment, long messageUniqueId)
    {
        return new AttachmentEntity
        {
            MessageUniqueId = messageUniqueId,
            Data = Convert.FromBase64String(attachment.Base64Data)
        };
    }

    public static ConversationEntity ToEntity(TextConversation textConversation, string deviceId)
    {
        var latestMessage = textConversation.Messages.OrderByDescending(m => m.Timestamp).First();

        Debug.WriteLine($"latestMessage: {latestMessage.Body} AddressesJson: {JsonSerializer.Serialize(textConversation.Recipients)}");
        return new ConversationEntity
        {
            ThreadId = textConversation.ThreadId,
            DeviceId = deviceId,
            AddressesJson = JsonSerializer.Serialize(textConversation.Recipients),
            HasRead = textConversation.Messages.Any(m => m.Read),
            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastMessageTimestamp = latestMessage.Timestamp,
            LastMessage = latestMessage.Body
        };
    }
    #endregion
} 
