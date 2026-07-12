using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Contracts;

public interface ISmsFeature : IFeature
{
    event EventHandler<(string DeviceId, long ThreadId)>? ConversationRemoved;

    event EventHandler<(string DeviceId, long ThreadId, Conversation Conversation, IReadOnlyList<Message> NewMessages)>? ConversationUpdated;

    Task<List<Conversation>> LoadConversationAsync(string deviceId);

    Task HandleTextMessage(string deviceId, ConversationInfo textConversation);

    Task<List<Message>> LoadMessagesForConversation(string deviceId, long threadId);

    Task RequestThreadHistory(PairedDevice device, long threadId, long rangeStartTimestamp = -1, long numberToRequest = -1);
}
