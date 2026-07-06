using Sefirah.Data.AppDatabase.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class SmsRepository(DatabaseContext context, ILogger logger)
{
    #region Conversation Operations

    public async Task<ConversationEntity?> GetConversationAsync(string deviceId, long threadId)
    {
        return await Task.Run(() =>
            context.Database.Find<ConversationEntity>(ConversationEntity.GetKey(deviceId, threadId)));
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
            var conversationKey = ConversationEntity.GetKey(deviceId, threadId);
            await Task.Run(() =>
            {
                context.Database.Delete<ConversationEntity>(conversationKey);
                context.Database.Table<MessageEntity>().Where(m => m.ConversationKey == conversationKey).Delete();
            });
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"Error deleting conversation {threadId} for device {deviceId}", ex);
            return false;
        }
    }

    public void DeleteAllDataForDevice(string deviceId)
    {
        var messageKeys = context.Database.Table<MessageEntity>()
            .Where(m => m.DeviceId == deviceId)
            .Select(m => m.Key)
            .ToList();

        context.Database.Table<AttachmentEntity>()
            .Where(a => messageKeys.Contains(a.MessageKey))
            .Delete();

        context.Database.Table<MessageEntity>().Where(m => m.DeviceId == deviceId).Delete();
        context.Database.Table<ConversationEntity>().Where(c => c.DeviceId == deviceId).Delete();

        logger.Info($"Deleted all SMS data for device {deviceId}");
    }

    #endregion

    #region Message Operations

    public async Task<List<MessageEntity>> GetMessagesAsync(string deviceId, long threadId)
    {
        var conversationKey = ConversationEntity.GetKey(deviceId, threadId);
        return await Task.Run(() =>
            context.Database.Table<MessageEntity>()
                .Where(m => m.ConversationKey == conversationKey)
                .OrderBy(m => m.Timestamp)
                .ToList());
    }

    public async Task<List<MessageEntity>> GetMessagesWithAttachmentsAsync(string deviceId, long threadId)
    {
        try
        {
            return await GetMessagesAsync(deviceId, threadId);
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting messages with attachments for device {deviceId}, thread {threadId}", ex);
            return [];
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
            logger.Error("Error saving batch messages", ex);
            return false;
        }
    }

    #endregion

    #region Attachment Operations

    public async Task<List<AttachmentEntity>> GetAttachmentsAsync(string messageKey)
    {
        try
        {
            return await Task.Run(() =>
                context.Database.Table<AttachmentEntity>()
                    .Where(a => a.MessageKey == messageKey)
                    .ToList());
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting attachments for message {messageKey}", ex);
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
            logger.Error($"Error saving attachment for message {attachment.MessageKey}", ex);
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
            logger.Error("Error saving batch attachments", ex);
            return false;
        }
    }

    #endregion
}
