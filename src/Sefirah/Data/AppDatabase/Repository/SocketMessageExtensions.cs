using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public static class SocketMessageExtensions
{
    public static MessageEntity ToEntity(this TextMessage message, string deviceId)
        => MessageEntity.FromMessage(message, deviceId);

    public static ConversationEntity ToEntity(this ConversationInfo thread, string deviceId)
        => ConversationEntity.FromMessage(thread, deviceId);

}
