using System.Text.Json;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
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

    public async Task<SmsConversationEntity?> GetConversationAsync(string deviceId, long threadId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<SmsConversationEntity>()
                    .FirstOrDefault(c => c.DeviceId == deviceId && c.ThreadId == threadId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting conversation for device {DeviceId}, thread {ThreadId}", deviceId, threadId);
            return null;
        }
    }

    public async Task<List<SmsConversationEntity>> GetConversationsAsync(string deviceId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<SmsConversationEntity>()
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

    public async Task<bool> SaveConversationAsync(SmsConversationEntity conversation)
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
                databaseContext.Database.Delete<SmsConversationEntity>(threadId);
                
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

    public async Task<List<TextMessageEntity>> GetMessagesAsync(string deviceId, long threadId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<TextMessageEntity>()
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

    public async Task<List<TextMessageEntity>> GetMessagesWithAttachmentsAsync(string deviceId, long threadId)
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

    public async Task<TextMessageEntity?> GetMessageAsync(string deviceId, long uniqueId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<TextMessageEntity>()
                    .FirstOrDefault(m => m.DeviceId == deviceId && m.UniqueId == uniqueId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting message {UniqueId} for device {DeviceId}", uniqueId, deviceId);
            return null;
        }
    }

    public async Task<bool> SaveMessageAsync(TextMessageEntity message)
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

    public async Task<bool> SaveMessagesAsync(List<TextMessageEntity> messages)
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

    public async Task<ContactEntity?> GetContactAsync(string deviceId, string phoneNumber)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<ContactEntity>()
                    .FirstOrDefault(c => c.DeviceId == deviceId && c.PhoneNumber == phoneNumber));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting contact {PhoneNumber} for device {DeviceId}", phoneNumber, deviceId);
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
            logger.LogError(ex, "Error saving contact {PhoneNumber} for device {DeviceId}", contact.PhoneNumber, contact.DeviceId);
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

    public async Task<List<SmsAttachmentEntity>> GetAttachmentsAsync(long messageUniqueId)
    {
        try
        {
            return await Task.Run(() => 
                databaseContext.Database.Table<SmsAttachmentEntity>()
                    .Where(a => a.MessageUniqueId == messageUniqueId)
                    .ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting attachments for message {MessageUniqueId}, device {DeviceId}", messageUniqueId);
            return [];
        }
    }

    public async Task<bool> SaveAttachmentAsync(SmsAttachmentEntity attachment)
    {
        try
        {
            await Task.Run(() => databaseContext.Database.InsertOrReplace(attachment));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving attachment for message {MessageUniqueId}, device {DeviceId}", attachment.MessageUniqueId);
            return false;
        }
    }

    public async Task<bool> SaveAttachmentsAsync(List<SmsAttachmentEntity> attachments)
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

    public static TextMessageEntity ToEntity(TextMessage message, string deviceId)
    {
        return new TextMessageEntity
        {
            UniqueId = message.UniqueId,
            ThreadId = message.ThreadId ?? 0,
            DeviceId = deviceId,
            Body = message.Body,
            Timestamp = message.Timestamp,
            Read = message.Read,
            SubscriptionId = message.SubscriptionId,
            MessageType = message.MessageType,
            AddressesJson = message.Addresses.Count > 0 ? JsonSerializer.Serialize(message.Addresses) : null,
            ContactsJson = message.Contacts.Count > 0 ? JsonSerializer.Serialize(message.Contacts) : null,
            Addresses = message.Addresses,
            Contacts = message.Contacts
        };
    }

    public static TextMessage FromEntity(TextMessageEntity entity)
    {
        var message = new TextMessage
        {
            UniqueId = entity.UniqueId,
            ThreadId = entity.ThreadId,
            Body = entity.Body,
            Timestamp = entity.Timestamp,
            Read = entity.Read,
            SubscriptionId = entity.SubscriptionId,
            MessageType = entity.MessageType,
            Addresses = entity.Addresses,
            Contacts = entity.Contacts,
            Attachments = entity.Attachments
        };

        // Deserialize JSON fields
        if (!string.IsNullOrEmpty(entity.AddressesJson))
        {
            try
            {
                message.Addresses = JsonSerializer.Deserialize<List<string>>(entity.AddressesJson) ?? [];
                entity.Addresses = message.Addresses;
            }
            catch (Exception)
            {
                message.Addresses = [];
            }
        }

        if (!string.IsNullOrEmpty(entity.ContactsJson))
        {
            try
            {
                message.Contacts = JsonSerializer.Deserialize<List<Contact>>(entity.ContactsJson) ?? [];
                entity.Contacts = message.Contacts;
            }
            catch (Exception)
            {
                message.Contacts = [];
            }
        }

        return message;
    }

    public static SmsConversationEntity ToEntity(SmsConversation conversation, string deviceId)
    {
        return new SmsConversationEntity
        {
            ThreadId = conversation.ThreadId,
            DeviceId = deviceId,
            LastMessageTimestamp = conversation.LastMessageTimestamp,
            LastMessageBody = conversation.LastMessage,
            HasRead = true, // TODO: Implement proper read status
            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            DisplayName = conversation.DisplayName
        };
    }

    public static ContactEntity ToEntity(Contact contact, string deviceId)
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
            DeviceId = deviceId,
            ContactName = contact.ContactName,
            PhoneNumber = contact.PhoneNumber,
            Avatar = avatar
        };
    }

    public static SmsAttachmentEntity ToEntity(SmsAttachment attachment, long messageUniqueId)
    {
        return new SmsAttachmentEntity
        {
            MessageUniqueId = messageUniqueId,
            Data = Convert.FromBase64String(attachment.Base64Data)
        };
    }

    #endregion
} 
