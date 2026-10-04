namespace Sefirah.Data.Models.Messages;

public readonly record struct ConversationUpdate(
    string DeviceId,
    long ThreadId,
    Conversation Conversation,
    IReadOnlyList<Message> Messages,
    IReadOnlyList<long> RemovedMessageIds,
    IReadOnlyList<MessageIdRemap> MessageIdRemaps);

/// <summary>Optimistic bubble confirm: local message id → Telephony id; optional temp thread promote.</summary>
public readonly record struct MessageIdRemap(long TempMessageId, long UniqueId, long? TempThreadId = null);
