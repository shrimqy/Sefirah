using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Utils;

namespace Sefirah.Features;

public class SmsFeature(
    SmsRepository smsRepository,
    ContactRepository contactRepository,
    ISessionManager sessionManager,
    ILogger logger) : ISmsFeature
{
    private readonly SemaphoreSlim semaphore = new(1, 1);
    private readonly List<QueuedMessage> queuedMessages = [];
    private readonly Lock queuedLock = new();
    private readonly Lock attachmentLock = new();
    private readonly Dictionary<(string DeviceId, long PartId), TaskCompletionSource<string?>> attachmentRequests = [];
    private readonly Lock requestedMessagesLock = new();
    private readonly HashSet<(string DeviceId, long ThreadId, long UniqueId)> requestedMessages = [];
    private static readonly TimeSpan AttachmentDownloadTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan RequestedMessagesTimeout = TimeSpan.FromSeconds(30);

    public Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        return Task.CompletedTask;
    }

    // Flush offline queue, then ask the phone which threads changed since our last checksums.
    private async void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        try
        {
            if (!device.IsConnected)
            {
                CancelAttachmentRequests(device.Id);
                ReleaseRequestedMessages(device.Id);
                return;
            }

            SendQueuedMessages(device);
            var conversations = await smsRepository.GetConversationsAsync(device.Id);
            device.SendMessage(new ConversationsRequest
            {
                KnownThreads = [.. conversations.Select(c => new ThreadInfo { ThreadId = c.ThreadId, Checksum = c.Checksum })]
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error requesting conversations from device {device.Id}", ex);
        }
    }

    public event EventHandler<(string DeviceId, long ThreadId)>? ConversationRemoved;
    public event EventHandler<ConversationUpdate>? ConversationUpdated;
    public event EventHandler<string>? QueuedMessagesSent;

    public async Task<List<Conversation>> LoadConversationAsync(string deviceId)
    {
        try
        {
            var conversationEntities = await smsRepository.GetConversationsAsync(deviceId);
            var list = new List<Conversation>();
            foreach (var entity in conversationEntities)
                list.Add(entity.ToConversation(contactRepository));

            // Rebuild temp sidebar rows from in-memory outbound queue for this device.
            foreach (var temp in GetTemporaryConversationsFromQueue(deviceId))
            {
                if (list.Any(c => c.ThreadId == temp.ThreadId))
                    continue;
                list.Insert(0, temp);
            }

            return list;
        }
        catch (Exception ex)
        {
            logger.Error($"Error loading conversations from database for device: {deviceId}", ex);
            return [];
        }
    }

    /// <summary>New/changed thread: recipients, checksum, tip body → sidebar row.</summary>
    public async Task HandleConversationInfo(string deviceId, ConversationInfo info)
    {
        await semaphore.WaitAsync();
        try
        {
            var entity = await GetOrCreateConversation(deviceId, info.ThreadId);
            if (info.Recipients.Count > 0)
                entity.AddressesJson = JsonSerializer.Serialize(info.Recipients);

            entity.Checksum = info.Checksum;
            var saved = await UpsertMessages(deviceId, info.ThreadId, [info.Message]);
            await UpdateConversationPreview(entity, deviceId, info.ThreadId, [info.Message]);
            await smsRepository.SaveConversationAsync(entity);
            var remaps = ConfirmQueuedMessages(deviceId, info.ThreadId, [info.Message]);
            RaiseConversationUpdated(deviceId, info.ThreadId, entity, messageIndex: null, saved, removed: [], remaps);
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling ConversationInfo {info.ThreadId} for device {deviceId}", ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>
    /// Message bodies. Open applies inventory + tip preview; New refreshes tip;
    /// History is scroll-only (preview untouched unless the row is empty).
    /// </summary>
    public async Task HandleMessageList(string deviceId, MessageList list)
    {
        await semaphore.WaitAsync();
        try
        {
            var textMessages = list.Messages.ToList();
            var messageIndex = list.Kind is MessageListKind.Open && list.MessageIds is { } ids
                ? new ThreadMessageIndex(ids)
                : null;
            if (messageIndex is not null)
                textMessages.RemoveAll(m => !messageIndex.Contains(m.UniqueId));

            var entity = await GetOrCreateConversation(deviceId, list.ThreadId);
            IReadOnlyList<long> removed = messageIndex is not null
                ? await smsRepository.ApplyMessageIndexAsync(deviceId, list.ThreadId, list.MessageIds!)
                : [];

            var saved = await UpsertMessages(deviceId, list.ThreadId, textMessages);

            if (list.Checksum is long checksum)
                entity.Checksum = checksum;

            if (list.Kind is not MessageListKind.History || entity.LastMessageTimestamp == 0)
            {
                await UpdateConversationPreview(entity, deviceId, list.ThreadId, textMessages);
                await smsRepository.SaveConversationAsync(entity);
            }

            var remaps = ConfirmQueuedMessages(deviceId, list.ThreadId, textMessages);

            RaiseConversationUpdated(deviceId, list.ThreadId, entity, messageIndex, saved, removed, remaps);

            if (list.Kind is MessageListKind.History)
                ReleaseRequestedMessages(deviceId, list.ThreadId, textMessages);
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling MessageList {list.ThreadId} for device {deviceId}", ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    /// <summary>Phone membership + checksum after a delete (no body page).</summary>
    public async Task HandleMessageIndex(string deviceId, MessageIndex index)
    {
        await semaphore.WaitAsync();
        try
        {
            var entity = await GetOrCreateConversation(deviceId, index.ThreadId);
            var removed = await smsRepository.ApplyMessageIndexAsync(deviceId, index.ThreadId, index.MessageIds);
            entity.Checksum = index.Checksum;
            await UpdateConversationPreview(entity, deviceId, index.ThreadId, []);
            await smsRepository.SaveConversationAsync(entity);
            RaiseConversationUpdated(
                deviceId, index.ThreadId, entity,
                messageIndex: new ThreadMessageIndex(index.MessageIds), saved: [], removed, remaps: []);
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling MessageIndex {index.ThreadId} for device {deviceId}", ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task HandleRemoveConversation(string deviceId, RemoveConversation removed)
    {
        await semaphore.WaitAsync();
        try
        {
            if (!await smsRepository.DeleteConversationAsync(deviceId, removed.ThreadId))
                return;
            lock (queuedLock)
                queuedMessages.RemoveAll(p => p.DeviceId == deviceId && p.Message.ThreadId == removed.ThreadId);
            ConversationRemoved?.Invoke(this, (deviceId, removed.ThreadId));
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling removed conversation {removed.ThreadId} for device {deviceId}", ex);
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<ConversationEntity> GetOrCreateConversation(string deviceId, long threadId)
    {
        return await smsRepository.GetConversationAsync(deviceId, threadId)
            ?? new ConversationEntity
            {
                Key = ConversationEntity.GetKey(deviceId, threadId),
                DeviceId = deviceId,
                ThreadId = threadId,
                TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
    }

    private void RaiseConversationUpdated(
        string deviceId,
        long threadId,
        ConversationEntity entity,
        ThreadMessageIndex? messageIndex,
        List<MessageEntity> saved,
        IReadOnlyList<long> removed,
        IReadOnlyList<MessageIdRemap> remaps)
    {
        var conversation = entity.ToConversation(contactRepository);
        conversation.MessageIndex = messageIndex;
        ConversationUpdated?.Invoke(this, new ConversationUpdate(
            deviceId,
            threadId,
            conversation,
            saved.Select(e => e.ToMessage(contactRepository)).ToList(),
            removed,
            remaps));
    }

    // Denormalized sidebar tip from the newest body in this packet, else latest row in DB.
    private async Task UpdateConversationPreview(
        ConversationEntity conversationEntity,
        string deviceId,
        long threadId,
        List<TextMessage> textMessages)
    {
        var latest = textMessages.OrderByDescending(m => m.Timestamp).FirstOrDefault();
        if (latest is null)
        {
            var fromDb = await smsRepository.GetNewestMessageAsync(deviceId, threadId);
            if (fromDb is null)
                return;
            conversationEntity.LastMessageTimestamp = fromDb.Timestamp;
            conversationEntity.LastMessage = Message.PreviewText(
                fromDb.Body,
                fromDb.AttachmentEntities.Select(a => a.MimeType));
            conversationEntity.HasRead = fromDb.Read;
            return;
        }
        conversationEntity.LastMessageTimestamp = latest.Timestamp;
        conversationEntity.LastMessage = Message.PreviewText(
            latest.Body,
            latest.Attachments?.Select(a => a.MimeType));
        conversationEntity.HasRead = latest.Read;
    }

    // Map wire TextMessages → entities (thumbnails + attachment rows) and upsert.
    private async Task<List<MessageEntity>> UpsertMessages(string deviceId, long threadId, List<TextMessage> textMessages)
    {
        if (textMessages.Count == 0)
            return [];

        var messageEntities = textMessages
            .Select(m =>
            {
                var entity = m.ToEntity(deviceId, threadId);
                if (m.Attachments is { Count: > 0 })
                {
                    entity.AttachmentEntities = [.. m.Attachments.Select(a =>
                    {
                        AttachmentStorage.SaveThumbnail(deviceId, m.UniqueId, a.PartId, a.FileName, a.Thumbnail);
                        return new AttachmentEntity
                        {
                            MessageKey = entity.Key,
                            FileName = a.FileName,
                            MimeType = a.MimeType,
                            PartId = a.PartId
                        };
                    })];
                }
                return entity;
            })
            .ToList();
        return await smsRepository.UpsertMessagesAsync(messageEntities);
    }

    public void QueueSendingMessage(PairedDevice device, Message uiMessage, IReadOnlyList<string> addresses)
    {
        lock (queuedLock)
            queuedMessages.Add(new QueuedMessage(device.Id, uiMessage, addresses));

        if (device.IsConnected)
        {
            uiMessage.SendState = OutgoingSendState.Sending;
            device.SendMessage(ToOutboundTextMessage(uiMessage, addresses));
        }
        else
        {
            uiMessage.SendState = OutgoingSendState.Queued;
        }
    }

    public IReadOnlyList<Message> GetQueuedMessages(string deviceId, long threadId)
    {
        lock (queuedLock)
        {
            return [.. queuedMessages
                .Where(p => p.DeviceId == deviceId && p.Message.ThreadId == threadId)
                .OrderBy(p => p.Message.Timestamp)
                .Select(p => p.Message)];
        }
    }

    private void SendQueuedMessages(PairedDevice device)
    {
        List<QueuedMessage> queued;
        lock (queuedLock)
        {
            queued = [.. queuedMessages.Where(p =>
                p.DeviceId == device.Id && p.Message.SendState is OutgoingSendState.Queued)];
        }
        if (queued.Count == 0)
            return;
        foreach (var item in queued)
        {
            item.Message.SendState = OutgoingSendState.Sending;
            device.SendMessage(ToOutboundTextMessage(item.Message, item.Addresses));
        }
        QueuedMessagesSent?.Invoke(this, device.Id);
    }

    private static TextMessage ToOutboundTextMessage(Message message, IReadOnlyList<string> addresses)
    {
        List<SmsAttachment>? attachments = null;
        if (message.Attachments.Count > 0)
        {
            attachments = [];
            foreach (var attachment in message.Attachments)
            {
                string? base64 = null;
                if (!string.IsNullOrEmpty(attachment.FilePath) && File.Exists(attachment.FilePath))
                    base64 = Convert.ToBase64String(File.ReadAllBytes(attachment.FilePath));

                attachments.Add(new SmsAttachment
                {
                    FileName = attachment.FileName,
                    MimeType = attachment.MimeType,
                    PartId = attachment.PartId,
                    Base64EncodedFile = base64,
                });
            }
        }

        return new TextMessage
        {
            UniqueId = message.UniqueId,
            Body = message.Body,
            Timestamp = message.Timestamp,
            MessageType = message.MessageType,
            Read = message.Read,
            SubscriptionId = message.SubscriptionId,
            Addresses = [.. addresses],
            Attachments = attachments,
        };
    }

    private List<Conversation> GetTemporaryConversationsFromQueue(string deviceId)
    {
        List<QueuedMessage> items;
        lock (queuedLock)
            items = [.. queuedMessages.Where(p => p.DeviceId == deviceId && p.Message.ThreadId < 0)];

        return [.. items
            .GroupBy(p => p.Message.ThreadId)
            .Select(group =>
            {
                var latest = group.OrderByDescending(p => p.Message.Timestamp).First();
                var addresses = latest.Addresses;
                return new Conversation
                {
                    ConversationKey = $"queued:{deviceId}:{group.Key}",
                    ThreadId = group.Key,
                    Contacts = [.. addresses.Select(a => contactRepository.GetContact(deviceId, a))],
                    LastMessage = Message.PreviewText(
                        latest.Message.Body,
                        latest.Message.Attachments.Select(a => a.MimeType)),
                    LastMessageTimestamp = latest.Message.Timestamp,
                    HasRead = true,
                };
            })
            .OrderByDescending(c => c.LastMessageTimestamp)];
    }

    /// <summary>Morph optimistic bubbles when tip carries TempId (desktop outbound UniqueId).</summary>
    private List<MessageIdRemap> ConfirmQueuedMessages(
        string deviceId,
        long threadId,
        List<TextMessage> messages)
    {
        lock (queuedLock)
        {
            if (messages.Count == 0)
                return [];
            var outgoing = messages.Where(m => m.MessageType != 1).ToList();
            if (outgoing.Count == 0)
                return [];

            var remaps = new List<MessageIdRemap>();
            foreach (var confirmed in outgoing)
            {
                if (confirmed.TempId is not long tempId || tempId == 0)
                    continue;

                var index = queuedMessages.FindIndex(p =>
                    p.DeviceId == deviceId && p.Message.UniqueId == tempId);
                if (index < 0)
                    continue;

                var queued = queuedMessages[index].Message;
                var previousThreadId = queued.ThreadId;
                foreach (var attachment in queued.Attachments)
                {
                    AttachmentStorage.Move(
                        deviceId, tempId, confirmed.UniqueId,
                        attachment.PartId, attachment.FileName, attachment.MimeType);
                    attachment.FilePath = AttachmentStorage.GetPathIfExists(
                        deviceId, confirmed.UniqueId, attachment.PartId, attachment.FileName, attachment.MimeType);
                    attachment.PreviewPath = AttachmentStorage.GetThumbnailPathIfExists(
                        deviceId, confirmed.UniqueId, attachment.PartId, attachment.FileName);
                }

                queued.UniqueId = confirmed.UniqueId;
                queued.MessageKey = MessageEntity.GetKey(deviceId, threadId, confirmed.UniqueId);
                queued.ThreadId = threadId;
                queued.Timestamp = confirmed.Timestamp;
                queued.Read = confirmed.Read;
                queued.SendState = OutgoingSendState.None;
                remaps.Add(new MessageIdRemap(
                    tempId,
                    confirmed.UniqueId,
                    previousThreadId < 0 && previousThreadId != threadId ? previousThreadId : null));
                queuedMessages.RemoveAt(index);
            }
            return remaps;
        }
    }

    public async Task<List<Message>> LoadMessagesForConversation(string deviceId, long threadId)
    {
        var messageEntities = await smsRepository.GetMessagesWithAttachmentsAsync(deviceId, threadId);
        return [.. messageEntities.Select(e => e.ToMessage(contactRepository))];
    }

    /// <summary>
    /// Next page while scrolling up. With the phone index: the newest ids not on screen, served from SQLite,
    /// with only the missing ids requested from the phone (they arrive as History).
    /// Without it (offline, or Open still pending): the next older SQLite page.
    /// </summary>
    public async Task<List<Message>> LoadOlderMessagesAsync(
        PairedDevice device,
        long threadId,
        Message oldest,
        IReadOnlySet<long> shownIds,
        ThreadMessageIndex? messageIndex)
    {
        if (!device.IsConnected || messageIndex is null)
        {
            var older = await smsRepository.GetMessagesOlderThanAsync(device.Id, threadId, oldest.Timestamp, oldest.UniqueId);
            return [.. older.Select(e => e.ToMessage(contactRepository))];
        }

        var page = messageIndex.TakeNotShown(shownIds, SmsRepository.PageSize);
        var cached = await smsRepository.GetMessagesByIdsAsync(device.Id, threadId, page);
        var cachedIds = cached.Select(e => e.UniqueId).ToHashSet();
        RequestMessages(device, threadId, [.. page.Where(id => !cachedIds.Contains(id))]);
        return [.. cached.Select(e => e.ToMessage(contactRepository))];
    }

    public void RequestLatestMessages(PairedDevice device, long threadId)
    {
        if (!device.IsConnected) return;
        device.SendMessage(new MessagesRequest { ThreadId = threadId });
    }

    private void RequestMessages(PairedDevice device, long threadId, List<long> uniqueIds)
    {
        List<(string DeviceId, long ThreadId, long UniqueId)> keys = [];
        lock (requestedMessagesLock)
        {
            foreach (var id in uniqueIds)
            {
                if (requestedMessages.Add((device.Id, threadId, id)))
                    keys.Add((device.Id, threadId, id));
            }
        }
        if (keys.Count == 0)
            return;

        device.SendMessage(new MessagesRequest
        {
            ThreadId = threadId,
            MessageIds = [.. keys.Select(k => k.UniqueId)],
        });
        _ = ReleaseRequestedMessagesAfterTimeout(keys);
    }

    // Ids the phone never returns (deleted meanwhile) become requestable again.
    private async Task ReleaseRequestedMessagesAfterTimeout(List<(string DeviceId, long ThreadId, long UniqueId)> keys)
    {
        await Task.Delay(RequestedMessagesTimeout);
        lock (requestedMessagesLock)
        {
            foreach (var key in keys)
                requestedMessages.Remove(key);
        }
    }

    private void ReleaseRequestedMessages(string deviceId, long threadId, IEnumerable<TextMessage> received)
    {
        lock (requestedMessagesLock)
        {
            foreach (var message in received)
                requestedMessages.Remove((deviceId, threadId, message.UniqueId));
        }
    }

    private void ReleaseRequestedMessages(string deviceId)
    {
        lock (requestedMessagesLock)
            requestedMessages.RemoveWhere(k => k.DeviceId == deviceId);
    }

    public async Task<string?> GetAttachmentFileAsync(PairedDevice device, long partId)
    {
        if (partId < 0)
            return null;
        var cached = await smsRepository.FindAttachmentByPartIdAsync(device.Id, partId);
        if (cached is { } found)
        {
            var (attachment, message) = found;
            var path = AttachmentStorage.GetPathIfExists(device.Id, message.UniqueId, partId, attachment.FileName, attachment.MimeType);
            if (path is not null)
                return path;
        }
        var key = (DeviceId: device.Id, PartId: partId);
        TaskCompletionSource<string?> completion;
        bool ownsRequest;
        lock (attachmentLock)
        {
            if (!device.IsConnected)
                return null;
            ownsRequest = !attachmentRequests.TryGetValue(key, out completion!);
            if (ownsRequest)
            {
                completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                attachmentRequests.Add(key, completion);
            }
        }
        if (!ownsRequest)
            return await completion.Task;
        try
        {
            if (device.IsConnected && !completion.Task.IsCompleted)
            {
                device.SendMessage(new SmsAttachmentRequest { PartId = partId });
                await completion.Task.WaitAsync(AttachmentDownloadTimeout);
            }
            else
            {
                completion.TrySetResult(null);
            }
        }
        catch (TimeoutException)
        {
            logger.Warn($"SMS attachment download timed out for device {device.Id}, partId {partId}");
            completion.TrySetResult(null);
        }
        catch (Exception ex)
        {
            logger.Error($"Error requesting SMS attachment for device {device.Id}, partId {partId}", ex);
            completion.TrySetResult(null);
        }
        finally
        {
            lock (attachmentLock)
            {
                if (attachmentRequests.TryGetValue(key, out var current) && ReferenceEquals(current, completion))
                    attachmentRequests.Remove(key);
            }
        }
        return await completion.Task;
    }

    private void CancelAttachmentRequests(string deviceId)
    {
        lock (attachmentLock)
        {
            foreach (var key in attachmentRequests.Keys.Where(k => k.DeviceId == deviceId).ToList())
            {
                attachmentRequests[key].TrySetResult(null);
                attachmentRequests.Remove(key);
            }
        }
    }

    public async Task HandleAttachmentFile(string deviceId, long partId, StorageFile? file)
    {
        string? path = null;
        await semaphore.WaitAsync();
        try
        {
            if (file is null)
                return;
            var found = await smsRepository.FindAttachmentByPartIdAsync(deviceId, partId);
            if (found is null)
            {
                logger.Warn($"No SMS attachment found for partId {partId} on device {deviceId}");
                return;
            }
            var (attachment, message) = found.Value;
            AttachmentStorage.CopyTo(file.Path, deviceId, message.UniqueId, partId, attachment.FileName, attachment.MimeType);
            AttachmentStorage.Delete(file.Path);
            path = AttachmentStorage.GetPathIfExists(deviceId, message.UniqueId, partId, attachment.FileName, attachment.MimeType);
        }
        catch (Exception ex)
        {
            logger.Error($"Error handling SMS attachment file for partId {partId}", ex);
        }
        finally
        {
            semaphore.Release();
            lock (attachmentLock)
            {
                if (attachmentRequests.TryGetValue((deviceId, partId), out var completion))
                    completion.TrySetResult(path);
            }
        }
    }
}
