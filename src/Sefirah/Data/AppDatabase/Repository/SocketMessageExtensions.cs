using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.AppDatabase.Repository;

public static class SocketMessageExtensions
{
    public static MessageEntity ToEntity(this TextMessage message, string deviceId, long threadId)
        => MessageEntity.FromMessage(message, deviceId, threadId);
}
