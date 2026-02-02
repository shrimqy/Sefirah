using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class SmsRepository(DatabaseContext context, ILogger logger)
{
    #region Conversation Operations

    public async Task<ConversationEntity?> GetConversationAsync(string deviceId, long threadId)
    {
        return await Task.Run(() => 
            context.Database.Table<ConversationEntity>()
                .FirstOrDefault(c => c.DeviceId == deviceId && c.ThreadId == threadId));
    }

    public async Task<List<ConversationEntity>> GetConversationsAsync(string deviceId)
    {
        return await Task.Run(() => 
            context.Database.Table<ConversationEntity>()
                .Where(c => c.DeviceId == deviceId)
                .OrderByDescending(c => c.LastMessageTimestamp)
                .ToList());
    }

    public async Task SaveConversationAsync(ConversationEntity conversation)
    {
        await Task.Run(() => context.Database.InsertOrReplace(conversation));
    }

    public async Task<bool> DeleteConversationAsync(string deviceId, long threadId)
    {
        try
        {
            await Task.Run(() =>
            {
                // Delete conversation
                context.Database.Delete<ConversationEntity>(threadId);
                
                // Delete associated messages
                context.Database.Execute("DELETE FROM TextMessageEntity WHERE DeviceId = ? AND ThreadId = ?", deviceId, threadId);
                
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting conversation {ThreadId} for device {DeviceId}", threadId, deviceId);
            return false;
        }
    }

    /// <summary>
    /// Deletes all SMS data (conversations, messages, contacts, attachments) for a device.
    /// Call when a device is removed.
    /// </summary>
    public void DeleteAllDataForDevice(string deviceId)
    {
        // Order matters: attachments reference messages, so delete attachments first.
        var messageIds = context.Database.Table<MessageEntity>()
            .Where(m => m.DeviceId == deviceId)
            .Select(m => m.UniqueId)
            .ToList();
        context.Database.Table<AttachmentEntity>()
            .Where(a => messageIds.Contains(a.MessageUniqueId))
            .Delete();

        context.Database.Table<MessageEntity>().Where(m => m.DeviceId == deviceId).Delete();
        context.Database.Table<ConversationEntity>().Where(c => c.DeviceId == deviceId).Delete();
        context.Database.Table<ContactEntity>().Where(c => c.DeviceId == deviceId).Delete();

        logger.LogInformation("Deleted all SMS data for device {DeviceId}", deviceId);
    }

    #endregion

    #region Message Operations

    public async Task<List<MessageEntity>> GetMessagesAsync(string deviceId, long threadId)
    {
        return await Task.Run(() => 
            context.Database.Table<MessageEntity>()
                .Where(m => m.DeviceId == deviceId && m.ThreadId == threadId)
                .OrderBy(m => m.Timestamp)
                .ToList());
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
            return context.Database.Table<MessageEntity>().Where(m => m.DeviceId == deviceId && m.UniqueId == uniqueId).ToList();
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
            await Task.Run(() => context.Database.InsertOrReplace(message));
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
            await Task.Run(() => context.Database.InsertAll(messages, "OR REPLACE"));
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
                context.Database.Execute("DELETE FROM TextMessageEntity WHERE DeviceId = ? AND UniqueId = ?", deviceId, uniqueId);
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

    public async Task<List<ContactEntity>> GetAllContactsAsync()
    {
        return await Task.Run(() => context.Database.Table<ContactEntity>().ToList());
    }

    public async Task<List<ContactEntity>> GetContactsForDevice(string deviceId)
    {
        return await Task.Run(() => context.Database.Table<ContactEntity>()
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(n => n.DisplayName)
            .ToList());
    }

    public async Task<ContactEntity?> GetContactAsync(string deviceId, string phoneNumber)  
    {
        return await Task.Run(() => context.Database.Table<ContactEntity>()
            .FirstOrDefault(c => c.DeviceId == deviceId && c.Number == phoneNumber));
    }

    public async Task<ContactEntity?> GetContactByIdAsync(string deviceId, string contactId)
    {
        return await Task.Run(() => context.Database.Table<ContactEntity>()
            .FirstOrDefault(c => c.DeviceId == deviceId && c.Id == contactId));
    }

    public async Task SaveContactAsync(string deviceId, ContactInfo contact)
    {
        var contactEntity = contact.ToEntity(deviceId);
        await Task.Run(() => context.Database.InsertOrReplace(contactEntity));
    }

    #endregion

    #region Attachment Operations

    public async Task<List<AttachmentEntity>> GetAttachmentsAsync(long messageUniqueId)
    {
        try
        {
            return await Task.Run(() => 
                context.Database.Table<AttachmentEntity>()
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
            await Task.Run(() => context.Database.InsertOrReplace(attachment));
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
            await Task.Run(() => context.Database.InsertAll(attachments, "OR REPLACE"));
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving batch attachments");
            return false;
        }
    }

    #endregion
} 
