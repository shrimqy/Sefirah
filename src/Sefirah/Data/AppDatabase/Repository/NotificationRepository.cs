using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class NotificationRepository(DatabaseContext context)
{
    public Task SaveNotificationAsync(NotificationInfo message, string deviceId) =>
        Task.Run(() =>
        {
            var existing = GetNotification(deviceId, message.NotificationKey);
            var entity = NotificationEntity.FromMessage(message, deviceId, existing?.Pinned ?? false);
            context.Database.InsertOrReplace(entity);
        });

    public async Task<List<Notification>> GetNotificationsAsync(string deviceId)
    {
        var entities = await Task.Run(() =>
            context.Database.Table<NotificationEntity>()
                .Where(n => n.DeviceId == deviceId)
                .OrderByDescending(n => n.Pinned)
                .ThenByDescending(n => n.TimestampMillis)
                .ToList());

        return entities.Select(e => e.ToNotification()).ToList();
    }

    public NotificationEntity? GetNotification(string deviceId, string key) =>
        context.Database.Find<NotificationEntity>(NotificationEntity.GetKey(deviceId, key));

    public Task UpdateNotificationPinAsync(Notification notification, string deviceId) =>
        Task.Run(() =>
        {
            var existing = GetNotification(deviceId, notification.Key);
            if (existing is null) return;
            existing.Pinned = notification.Pinned;
            context.Database.Update(existing);
        });

    /// <summary>
    /// Deletes the notification if it exists and is not pinned. Returns true if deleted, false otherwise.
    /// </summary>
    public async Task<bool> DeleteNotificationAsync(string deviceId, string key)
    {
        return await Task.Run(() =>
        {
            var existing = GetNotification(deviceId, key);
            if (existing is null || existing.Pinned) return false;
            return context.Database.Delete<NotificationEntity>(NotificationEntity.GetKey(deviceId, key)) > 0;
        });
    }

    /// <summary>
    /// Removes all notifications for the device (including pinned). Used when removing the device.
    /// </summary>
    public void RemoveNotificationsForDevice(string deviceId)
    {
        context.Database.Table<NotificationEntity>().Where(n => n.DeviceId == deviceId).Delete();
    }

    /// <summary>
    /// Removes notifications for the device (except pinned).
    /// </summary>
    public Task ClearHistoryForDeviceAsync(string deviceId) =>
        Task.Run(() => context.Database.Table<NotificationEntity>().Where(n => n.DeviceId == deviceId && !n.Pinned).Delete());
}
