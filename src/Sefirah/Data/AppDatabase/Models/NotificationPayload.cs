using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Models;

internal sealed class NotificationPayload
{
    public List<NotificationMessage> Messages { get; set; } = [];

    public List<NotificationAction> Actions { get; set; } = [];
}
