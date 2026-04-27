using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;

namespace Sefirah.Data.AppDatabase.Repository;

public class CallLogRepository(
    DatabaseContext context,
    ContactRepository contactRepository,
    ILogger logger)
{
    public event EventHandler<(string deviceId, CallLog callLog)>? CallLogUpdated;

    public async Task<List<CallLog>> GetCallLogsAsync(string deviceId)
    {
        try
        {
            var logEntities = await Task.Run(() => context.Database.Table<CallLogEntity>()
                .Where(log => log.DeviceId == deviceId)
                .OrderByDescending(log => log.TimestampMillis)
                .ToList());

            var logs = new List<CallLog>(logEntities.Count);
            foreach (var logEntity in logEntities)
            {
                var contact = contactRepository.GetCallerContactByPhoneNumber(logEntity.PhoneNumber);
                var log = logEntity.ToCallLogAsync(contact);
                logs.Add(log);
            }

            return logs;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load call logs for device {DeviceId}.", deviceId);
            return [];
        }
    }

    public async Task SaveCallLogAsync(string deviceId, CallLogInfo log)
    {
        try
        {
            var entity = CallLogEntity.FromModel(deviceId, log);
            await Task.Run(() => context.Database.InsertOrReplace(entity));

            if (log.ContactInfo is not null)
            {
                await contactRepository.SaveContactAsync(deviceId, log.ContactInfo);
            }

            var contact = contactRepository.GetCallerContactByPhoneNumber(log.PhoneNumber);

            var callLog = entity.ToCallLogAsync(contact);
            CallLogUpdated?.Invoke(this, (deviceId, callLog));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save call log for device {DeviceId}.", deviceId);
        }
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
            logger.LogError(ex, "Failed to delete call logs for device {DeviceId}", deviceId);
        }
    }
}
