using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class CallLogRepository(
    DatabaseContext context,
    ContactRepository contactRepository,
    ILogger logger)
{
    public const int MaxCallLogsPerDevice = 200;

    public event EventHandler<(string deviceId, CallLog callLog)>? CallLogUpdated;

    public async Task<List<CallLog>> GetCallLogsAsync(string deviceId)
    {
        try
        {
            var logEntities = await Task.Run(() =>
            {
                TrimCallLogs(deviceId);
                return context.Database.Table<CallLogEntity>()
                    .Where(log => log.DeviceId == deviceId)
                    .OrderByDescending(log => log.TimestampMillis)
                    .ToList();
            });

            var logs = new List<CallLog>(logEntities.Count);
            foreach (var logEntity in logEntities)
            {
                var contact = contactRepository.GetContact(logEntity.DeviceId, logEntity.PhoneNumber);
                var log = logEntity.ToCallLog(contact);
                logs.Add(log);
            }

            return logs;
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to load call logs for device {deviceId}.", ex);
            return [];
        }
    }

    public async Task SaveCallLogAsync(string deviceId, CallLogInfo log)
    {
        try
        {
            if (log.ContactInfo is not null)
                await contactRepository.SaveContactAsync(deviceId, log.ContactInfo);

            var entity = CallLogEntity.FromModel(deviceId, log);
            await Task.Run(() => context.Database.InsertOrReplace(entity));

            var contact = contactRepository.GetContact(deviceId, log.PhoneNumber);
            CallLogUpdated?.Invoke(this, (deviceId, entity.ToCallLog(contact)));
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to save call log for device {deviceId}.", ex);
        }
    }

    private void TrimCallLogs(string deviceId)
    {
        var count = context.Database.Table<CallLogEntity>().Count(log => log.DeviceId == deviceId);

        if (count <= MaxCallLogsPerDevice)
            return;

        var excess = count - MaxCallLogsPerDevice;
        context.Database.Execute(
            "DELETE FROM CallLogEntity WHERE DeviceId = ? AND Key IN (SELECT Key FROM CallLogEntity WHERE DeviceId = ? ORDER BY TimestampMillis ASC LIMIT ?)",
            deviceId, deviceId, excess);
    }

    /// <summary>
    /// Deletes all call log entries for a device. Call when a device is removed.
    /// </summary>
    public void DeleteAllCallLogsForDevice(string deviceId)
    {
        try
        {
            context.Database.Table<CallLogEntity>().Where(log => log.DeviceId == deviceId).Delete();
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to delete call logs for device {deviceId}", ex);
        }
    }
}
