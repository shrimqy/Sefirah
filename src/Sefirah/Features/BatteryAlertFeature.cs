using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.Models;

namespace Sefirah.Features;

public sealed class BatteryAlertFeature(
    ISessionManager sessionManager,
    IPlatformNotificationHandler platformNotificationHandler,
    ILogger<BatteryAlertFeature> logger) : IBatteryAlertFeature
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> deviceLocks = [];

    public Task InitializeAsync()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        return Task.CompletedTask;
    }

    public async Task HandleBatteryStateAsync(PairedDevice device, BatteryState batteryState)
    {
        var deviceLock = deviceLocks.GetOrAdd(device.Id, _ => new SemaphoreSlim(1, 1));
        await deviceLock.WaitAsync();

        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BatteryStatus = batteryState);

            var notificationTag = Constants.Notification.GetBatteryTag(device.Id);
            if (!ShouldShowLowBatteryAlert(batteryState, device.DeviceSettings.LowBatteryAlertThreshold))
            {
                device.DeviceSettings.LowBatteryAlertShown = false;
                await platformNotificationHandler.RemoveNotificationsByTagAndGroup(notificationTag, Constants.Notification.BatteryGroup);
                return;
            }

            if (!device.DeviceSettings.LowBatteryAlertsEnabled || device.DeviceSettings.LowBatteryAlertShown)
            {
                return;
            }

            var title = "BatteryNotification.Title".GetLocalizedResource();
            var text = string.Format("BatteryNotification.Text".GetLocalizedResource(), device.Name, batteryState.BatteryLevel);

            await platformNotificationHandler.ShowBatteryNotification(title, text, notificationTag);
            device.DeviceSettings.LowBatteryAlertShown = true;
            logger.LogInformation("Displayed low battery notification for device {DeviceId} at {BatteryLevel}%", device.Id, batteryState.BatteryLevel);
        }
        finally
        {
            deviceLock.Release();
        }
    }

    private async void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            return;
        }

        await platformNotificationHandler.RemoveNotificationsByTagAndGroup(Constants.Notification.GetBatteryTag(device.Id), Constants.Notification.BatteryGroup);
    }

    private static bool ShouldShowLowBatteryAlert(BatteryState batteryState, int threshold) =>
        batteryState.BatteryLevel <= threshold && !batteryState.IsCharging;
}
