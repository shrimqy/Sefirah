using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Extensions;

namespace Sefirah.Services;

public sealed class BatteryAlertService : IBatteryAlertService
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> deviceLocks = [];
    private readonly ConcurrentDictionary<string, bool> shownAlerts = [];
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
        await WithDeviceLockAsync(device.Id, async () =>
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BatteryStatus = batteryState);
            await ReconcileBatteryAlertStateCoreAsync(device, batteryState);
        });
    }

    public async Task ReconcileBatteryAlertStateAsync(PairedDevice device)
    {
        await WithDeviceLockAsync(device.Id, async () =>
        {
            await ReconcileBatteryAlertStateCoreAsync(device, device.BatteryStatus);
        });
    }

    private SemaphoreSlim GetDeviceLock(string deviceId) =>
        deviceLocks.GetOrAdd(deviceId, _ => new SemaphoreSlim(1, 1));

    private async Task WithDeviceLockAsync(string deviceId, Func<Task> action)
    {
        var deviceLock = GetDeviceLock(deviceId);
        await deviceLock.WaitAsync();

        try
        {
            await action();
        }
        finally
        {
            deviceLock.Release();
        }
    }

    private async Task ReconcileBatteryAlertStateCoreAsync(PairedDevice device, BatteryState? batteryState)
    {
        var notificationTag = Constants.Notification.GetBatteryTag(device.Id);
        if (!device.DeviceSettings.LowBatteryAlertsEnabled)
        {
            await ResetBatteryAlertStateCoreAsync(device.Id);
            return;
        }

        if (batteryState is null)
        {
            return;
        }

        if (!ShouldShowLowBatteryAlert(batteryState, device.DeviceSettings.LowBatteryAlertThreshold))
        {
            await ResetBatteryAlertStateCoreAsync(device.Id);
            return;
        }

        if (shownAlerts.ContainsKey(device.Id))
        {
            return;
        }

        var title = "BatteryNotification.Title".GetLocalizedResource();
        var text = string.Format("BatteryNotification.Text".GetLocalizedResource(), device.Name, batteryState.BatteryLevel);

        var shown = await platformNotificationHandler.ShowBatteryNotification(title, text, notificationTag);
        if (!shown)
        {
            return;
        }

        shownAlerts[device.Id] = true;
        logger.LogInformation(
            "Displayed low battery notification for device {DeviceId} at {BatteryLevel}% with threshold {Threshold}%",
            device.Id,
            batteryState.BatteryLevel,
            device.DeviceSettings.LowBatteryAlertThreshold);
    }

    private async void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (device.IsConnected)
        {
            return;
        }

        await WithDeviceLockAsync(device.Id, () => ResetBatteryAlertStateCoreAsync(device.Id));
    }

    private async Task ResetBatteryAlertStateCoreAsync(string deviceId)
    {
        shownAlerts.TryRemove(deviceId, out _);
        await platformNotificationHandler.RemoveNotificationByTag(Constants.Notification.GetBatteryTag(deviceId));
    }

    private static bool ShouldShowLowBatteryAlert(BatteryState batteryState, int threshold) =>
        batteryState.BatteryLevel <= threshold && !batteryState.IsCharging;
}
