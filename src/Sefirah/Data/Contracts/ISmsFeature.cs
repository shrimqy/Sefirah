using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Contracts;

public interface ISmsFeature : IFeature
{
    event EventHandler<(string DeviceId, long ThreadId)>? ConversationRemoved;

    event EventHandler<ConversationUpdate>? ConversationUpdated;

    event EventHandler<string>? QueuedMessagesSent;

    Task<List<Conversation>> LoadConversationAsync(string deviceId);

    Task HandleConversationInfo(string deviceId, ConversationInfo info);

    Task HandleMessageList(string deviceId, MessageList list);

    Task HandleMessageIndex(string deviceId, MessageIndex index);

    Task HandleRemoveConversation(string deviceId, RemoveConversation removed);

    Task<List<Message>> LoadMessagesForConversation(string deviceId, long threadId);

    Task<List<Message>> LoadOlderMessagesAsync(
        PairedDevice device,
        long threadId,
        Message oldest,
        IReadOnlySet<long> shownIds,
        ThreadMessageIndex? messageIndex);

    IReadOnlyList<Message> GetQueuedMessages(string deviceId, long threadId);

    void QueueSendingMessage(PairedDevice device, Message uiMessage, IReadOnlyList<string> addresses);

    void RequestLatestMessages(PairedDevice device, long threadId);

    Task<string?> GetAttachmentFileAsync(PairedDevice device, long partId);

    Task HandleAttachmentFile(string deviceId, long partId, StorageFile? file);
}
