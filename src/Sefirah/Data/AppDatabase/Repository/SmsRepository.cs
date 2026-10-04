using Sefirah.Data.AppDatabase.Models;
using Sefirah.Utils;

namespace Sefirah.Data.AppDatabase.Repository;

public class SmsRepository(DatabaseContext context, ILogger logger)
{
    public const int PageSize = 25;

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
                var messages = context.Database.Table<MessageEntity>()
                    .Where(m => m.ConversationKey == conversationKey)
                    .ToList();
                DeleteAttachments(messages);
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
        AttachmentStorage.DeleteAllForDevice(deviceId);

        logger.Info($"Deleted all SMS data for device {deviceId}");
    }

    #endregion

    #region Message Operations

    public async Task<MessageEntity?> GetNewestMessageAsync(string deviceId, long threadId)
    {
        var conversationKey = ConversationEntity.GetKey(deviceId, threadId);
        var newest = await Task.Run(() =>
            context.Database.Table<MessageEntity>()
                .Where(m => m.ConversationKey == conversationKey)
                .OrderByDescending(m => m.Timestamp)
                .ThenByDescending(m => m.UniqueId)
                .Take(1)
                .ToList());
        return (await AttachAttachmentsAsync(newest)).FirstOrDefault();
    }

    public async Task<List<MessageEntity>> GetMessagesWithAttachmentsAsync(string deviceId, long threadId)
    {
        try
        {
            var conversationKey = ConversationEntity.GetKey(deviceId, threadId);
            var messages = await Task.Run(() =>
                context.Database.Table<MessageEntity>()
                    .Where(m => m.ConversationKey == conversationKey)
                    .OrderByDescending(m => m.Timestamp)
                    .ThenByDescending(m => m.UniqueId)
                    .Take(PageSize)
                    .ToList());
            return await AttachAttachmentsAsync(messages);
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting messages with attachments for device {deviceId}, thread {threadId}", ex);
            throw;
        }
    }

    /// <summary>Next page strictly older than (beforeTimestamp, beforeUniqueId).</summary>
    public async Task<List<MessageEntity>> GetMessagesOlderThanAsync(
        string deviceId,
        long threadId,
        long beforeTimestamp,
        long beforeUniqueId)
    {
        try
        {
            var conversationKey = ConversationEntity.GetKey(deviceId, threadId);
            var messages = await Task.Run(() =>
                context.Database.Table<MessageEntity>()
                    .Where(m => m.ConversationKey == conversationKey &&
                        (m.Timestamp < beforeTimestamp ||
                         (m.Timestamp == beforeTimestamp && m.UniqueId < beforeUniqueId)))
                    .OrderByDescending(m => m.Timestamp)
                    .ThenByDescending(m => m.UniqueId)
                    .Take(PageSize)
                    .ToList());
            return await AttachAttachmentsAsync(messages);
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting older messages for device {deviceId}, thread {threadId}", ex);
            throw;
        }
    }

    public async Task<List<MessageEntity>> GetMessagesByIdsAsync(string deviceId, long threadId, IReadOnlyCollection<long> uniqueIds)
    {
        if (uniqueIds.Count == 0)
            return [];
        try
        {
            var keys = uniqueIds.Select(id => MessageEntity.GetKey(deviceId, threadId, id)).ToList();
            var messages = await Task.Run(() =>
                context.Database.Table<MessageEntity>()
                    .Where(m => keys.Contains(m.Key))
                    .ToList());
            return await AttachAttachmentsAsync(messages);
        }
        catch (Exception ex)
        {
            logger.Error($"Error getting messages by id for device {deviceId}, thread {threadId}", ex);
            throw;
        }
    }

    private async Task<List<MessageEntity>> AttachAttachmentsAsync(List<MessageEntity> messages)
    {
        if (messages.Count == 0)
            return messages;

        var keys = messages.Select(m => m.Key).ToList();
        var attachments = await Task.Run(() => QueryAttachments(keys));
        var byKey = attachments.GroupBy(a => a.MessageKey).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var message in messages)
        {
            if (byKey.TryGetValue(message.Key, out var list))
                message.AttachmentEntities = list;
        }

        return messages;
    }

    public async Task<List<MessageEntity>> UpsertMessagesAsync(List<MessageEntity> messages)
    {
        if (messages.Count == 0)
            return [];
        return await Task.Run(() =>
        {
            context.Database.RunInTransaction(() =>
            {
                context.Database.InsertAll(messages, "OR REPLACE");
                ReplaceAttachments(messages);
            });
            return messages;
        });
    }

    /// <summary>
    /// Drop local messages missing from the authoritative phone ID list.
    /// </summary>
    public async Task<List<long>> ApplyMessageIndexAsync(
        string deviceId,
        long threadId,
        IReadOnlyList<long> messageIds)
    {
        return await Task.Run(() =>
        {
            var removed = new List<long>();
            context.Database.RunInTransaction(() =>
            {
                var conversationKey = ConversationEntity.GetKey(deviceId, threadId);
                var ids = messageIds.ToHashSet();
                var existing = context.Database.Table<MessageEntity>()
                    .Where(m => m.ConversationKey == conversationKey)
                    .ToList();

                List<MessageEntity> toDelete = [];
                foreach (var message in existing)
                {
                    if (!ids.Contains(message.UniqueId))
                        toDelete.Add(message);
                }

                DeleteAttachments(toDelete);
                foreach (var message in toDelete)
                {
                    context.Database.Delete(message);
                    removed.Add(message.UniqueId);
                }
            });
            return removed;
        });
    }

    #endregion

    #region Attachment Operations

    private List<AttachmentEntity> QueryAttachments(List<string> messageKeys)
    {
        if (messageKeys.Count == 0)
            return [];

        return [.. context.Database.Table<AttachmentEntity>()
            .Where(a => messageKeys.Contains(a.MessageKey))];
    }

    private void ReplaceAttachments(List<MessageEntity> messages)
    {
        // An incoming record carries its complete attachment list, including an empty list.
        var withAttachments = messages;
        if (withAttachments.Count == 0)
            return;

        var keys = withAttachments.Select(m => m.Key).ToList();
        var byKey = withAttachments.ToDictionary(m => m.Key);
        var keep = withAttachments
            .SelectMany(m => m.AttachmentEntities.SelectMany(a => new[]
            {
                AttachmentStorage.GetPath(m.DeviceId, m.UniqueId, a.PartId, a.FileName, a.MimeType),
                AttachmentStorage.GetThumbnailPath(m.DeviceId, m.UniqueId, a.PartId, a.FileName)
            }))
            .ToHashSet();

        foreach (var existing in QueryAttachments(keys))
        {
            if (!byKey.TryGetValue(existing.MessageKey, out var message))
                continue;

            var path = AttachmentStorage.GetPath(message.DeviceId, message.UniqueId, existing.PartId, existing.FileName, existing.MimeType);
            var thumbPath = AttachmentStorage.GetThumbnailPath(message.DeviceId, message.UniqueId, existing.PartId, existing.FileName);
            if (!keep.Contains(path))
                AttachmentStorage.Delete(path);
            if (!keep.Contains(thumbPath))
                AttachmentStorage.Delete(thumbPath);
        }

        DeleteAttachmentRows(keys);
        context.Database.InsertAll(withAttachments.SelectMany(m => m.AttachmentEntities).ToList());
    }

    private void DeleteAttachments(List<MessageEntity> messages)
    {
        if (messages.Count == 0)
            return;

        var keys = messages.Select(m => m.Key).ToList();
        var byKey = messages.ToDictionary(m => m.Key);
        foreach (var attachment in QueryAttachments(keys))
        {
            if (!byKey.TryGetValue(attachment.MessageKey, out var message))
                continue;

            AttachmentStorage.DeleteAll(
                message.DeviceId, message.UniqueId, attachment.PartId, attachment.FileName, attachment.MimeType);
        }

        DeleteAttachmentRows(keys);
    }

    private void DeleteAttachmentRows(List<string> messageKeys)
    {
        if (messageKeys.Count == 0)
            return;

        context.Database.Table<AttachmentEntity>()
            .Where(a => messageKeys.Contains(a.MessageKey))
            .Delete();
    }

    public async Task<(AttachmentEntity Attachment, MessageEntity Message)?> FindAttachmentByPartIdAsync(string deviceId, long partId)
    {
        return await Task.Run<(AttachmentEntity, MessageEntity)?>(() =>
        {
            var candidates = context.Database.Table<AttachmentEntity>()
                .Where(a => a.PartId == partId)
                .ToList();

            foreach (var attachment in candidates)
            {
                var message = context.Database.Find<MessageEntity>(attachment.MessageKey);
                if (message?.DeviceId == deviceId)
                    return (attachment, message);
            }

            return null;
        });
    }
    #endregion
}
