using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Extensions;

namespace Sefirah.Services;

public sealed class BatteryAlertService : IBatteryAlertService
{
    private const int LowBatteryThreshold = 20;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> deviceLocks = [];
    private readonly IPlatformNotificationHandler platformNotificationHandler;
    private readonly ILogger<BatteryAlertService> logger;

    public BatteryAlertService(
        ISessionManager sessionManager,
        IPlatformNotificationHandler platformNotificationHandler,
        ILogger<BatteryAlertService> logger)
    {
        this.platformNotificationHandler = platformNotificationHandler;
        this.logger = logger;

        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
    }

    public async Task HandleBatteryStateAsync(PairedDevice device, BatteryState batteryState)
    {
        var deviceLock = deviceLocks.GetOrAdd(device.Id, _ => new SemaphoreSlim(1, 1));
        await deviceLock.WaitAsync();

        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BatteryStatus = batteryState);

            var notificationTag = Constants.Notification.GetBatteryTag(device.Id);
            if (!ShouldShowLowBatteryAlert(batteryState))
            {
                device.DeviceSettings.LowBatteryAlertShown = false;
                await platformNotificationHandler.RemoveNotificationByTag(notificationTag);
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

        await platformNotificationHandler.RemoveNotificationByTag(Constants.Notification.GetBatteryTag(device.Id));
    }

    private static bool ShouldShowLowBatteryAlert(BatteryState batteryState) =>
        batteryState.BatteryLevel <= LowBatteryThreshold && !batteryState.IsCharging;
}
