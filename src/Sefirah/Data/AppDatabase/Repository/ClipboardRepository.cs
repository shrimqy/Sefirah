using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Data.AppDatabase.Repository;

public class ClipboardRepository(DatabaseContext context)
{
    /// <summary>
    /// How much history to keep per device. Past this the oldest entries go, along with their images.
    /// </summary>
    private const int MaxEntries = 200;

    public Task SaveAsync(string deviceId, string clipboardType, string content, bool isImage) =>
        Task.Run(() =>
        {
            if (string.IsNullOrEmpty(content)) return;

            // The device re-sends the current clipboard on every reconnect; no point stacking duplicates
            var latest = context.Database.Table<ClipboardEntity>()
                .Where(entry => entry.DeviceId == deviceId)
                .OrderByDescending(entry => entry.TimestampMillis)
                .FirstOrDefault();

            if (latest is not null && latest.IsImage == isImage && latest.Content == content) return;

            context.Database.Insert(new ClipboardEntity
            {
                DeviceId = deviceId,
                ClipboardType = clipboardType,
                Content = content,
                IsImage = isImage,
                TimestampMillis = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            Trim(deviceId);
        });

    public async Task<List<ClipboardEntry>> GetEntriesAsync(string deviceId)
    {
        var entities = await Task.Run(() =>
            context.Database.Table<ClipboardEntity>()
                .Where(entry => entry.DeviceId == deviceId)
                .OrderByDescending(entry => entry.TimestampMillis)
                .ToList());

        return [.. entities.Select(ToEntry)];
    }

    public Task DeleteAsync(int id) =>
        Task.Run(() =>
        {
            var entity = context.Database.Find<ClipboardEntity>(id);
            if (entity is null) return;

            DeleteImage(entity);
            context.Database.Delete<ClipboardEntity>(id);
        });

    public Task ClearAsync(string deviceId) =>
        Task.Run(() => DeleteAllForDevice(deviceId));

    public void DeleteAllForDevice(string deviceId)
    {
        foreach (var entity in context.Database.Table<ClipboardEntity>().Where(entry => entry.DeviceId == deviceId).ToList())
        {
            DeleteImage(entity);
        }
        context.Database.Table<ClipboardEntity>().Delete(entry => entry.DeviceId == deviceId);
    }

    private void Trim(string deviceId)
    {
        var stale = context.Database.Table<ClipboardEntity>()
            .Where(entry => entry.DeviceId == deviceId)
            .OrderByDescending(entry => entry.TimestampMillis)
            .Skip(MaxEntries)
            .ToList();

        foreach (var entity in stale)
        {
            DeleteImage(entity);
            context.Database.Delete<ClipboardEntity>(entity.Id);
        }
    }

    private static void DeleteImage(ClipboardEntity entity)
    {
        if (!entity.IsImage) return;

        try
        {
            if (File.Exists(entity.Content)) File.Delete(entity.Content);
        }
        catch
        {
            // A leftover image is harmless, a crash while pruning is not
        }
    }

    private static ClipboardEntry ToEntry(ClipboardEntity entity) => new()
    {
        Id = entity.Id,
        ClipboardType = entity.ClipboardType,
        Content = entity.Content,
        IsImage = entity.IsImage,
        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(entity.TimestampMillis).LocalDateTime
    };
}
