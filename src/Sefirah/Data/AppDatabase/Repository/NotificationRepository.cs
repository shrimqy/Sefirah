using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class NotificationRepository(DatabaseContext context)
{
    public static string CompositeId(string deviceId, string key) => $"{deviceId}|{key}";

    public void SaveNotification(NotificationInfo message, string deviceId)
    {
        Task.Run(() =>
        {
            var entity = message.ToEntity(deviceId);
            context.Database.InsertOrReplace(entity);
        });
    }

    public async Task<List<NotificationEntity>> GetNotificationsAsync(string deviceId)
    {
        return await Task.Run(() =>
            context.Database.Table<NotificationEntity>().Where(n => n.DeviceId == deviceId)
                .OrderByDescending(n => n.Pinned)
                .ToList());
    }

    public NotificationEntity? GetNotification(string deviceId, string key)
    {
        return context.Database.Find<NotificationEntity>(CompositeId(deviceId, key));
    }

    public void UpdateNotificationPin(Notification notification, string deviceId)
    {
        Task.Run(() =>
        {
            var existing = GetNotification(deviceId, notification.Key);
            if (existing is null) return;
            existing.Pinned = notification.Pinned;
            context.Database.InsertOrReplace(existing);
        });
    }

    /// <summary>
    /// Deletes the notification if it exists and is not pinned. Returns true if deleted, false otherwise.
    /// </summary>
    public async Task<bool> DeleteNotificationAsync(string deviceId, string key)
    {
        return await Task.Run(() =>
        {
            var existing = GetNotification(deviceId, key);
            if (existing is null || existing.Pinned) return false; 
            return context.Database.Delete<NotificationEntity>(CompositeId(deviceId, key)) > 0;
        });
    }

    /// <summary>
    /// Removes all notifications for the device (including pinned). Used when removing the device.
    /// </summary>
    public void RemoveNotificationsForDevice(string deviceId)
    {
        Task.Run(() => context.Database.Table<NotificationEntity>().Where(n => n.DeviceId == deviceId).Delete());
    }

    /// <summary>
    /// Removes notifications for the device (except pinned).
    /// </summary>
    public void ClearHistoryForDevice(string deviceId)
    {
        Task.Run(() => context.Database.Table<NotificationEntity>().Where(n => n.DeviceId == deviceId && !n.Pinned).Delete());
    }
}
