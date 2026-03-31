using System.Collections.Concurrent;
using System.ComponentModel;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Extensions;

namespace Sefirah.Services;

public sealed class BatteryAlertService : IBatteryAlertService
{
    private const int LowBatteryThreshold = 20;

    private readonly ConcurrentDictionary<string, SemaphoreSlim> deviceLocks = [];
    private readonly ConcurrentDictionary<string, PropertyChangedEventHandler> settingsChangedHandlers = [];
    private readonly ConcurrentDictionary<string, byte> trackedDevices = [];
    private readonly IPlatformNotificationHandler platformNotificationHandler;
    private readonly ILogger<BatteryAlertService> logger;

    public BatteryAlertService(
        ISessionManager sessionManager,
        IDeviceManager deviceManager,
        IPlatformNotificationHandler platformNotificationHandler,
        ILogger<BatteryAlertService> logger)
    {
        this.platformNotificationHandler = platformNotificationHandler;
        this.logger = logger;

        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        deviceManager.DeviceRemoved += OnDeviceRemoved;
    }

    public async Task HandleBatteryStateAsync(PairedDevice device, BatteryState batteryState)
    {
        EnsureDeviceTracked(device);

        var deviceLock = deviceLocks.GetOrAdd(device.Id, _ => new SemaphoreSlim(1, 1));
        await deviceLock.WaitAsync();

        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => device.BatteryStatus = batteryState);

            var notificationTag = BuildNotificationTag(device.Id);
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

        await platformNotificationHandler.RemoveNotificationByTag(BuildNotificationTag(device.Id));
    }

    private async void OnDeviceRemoved(object? sender, PairedDevice device)
    {
        UntrackDevice(device);
        await platformNotificationHandler.RemoveNotificationByTag(BuildNotificationTag(device.Id));
    }

    private void EnsureDeviceTracked(PairedDevice device)
    {
        if (!trackedDevices.TryAdd(device.Id, 0))
        {
            return;
        }

        PropertyChangedEventHandler handler = (_, e) => OnDeviceSettingsChanged(device, e);
        settingsChangedHandlers[device.Id] = handler;
        device.DeviceSettings.PropertyChanged += handler;
    }

    private void OnDeviceSettingsChanged(PairedDevice device, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != nameof(IDeviceSettingsService.LowBatteryAlertsEnabled))
        {
            return;
        }

        if (device.DeviceSettings.LowBatteryAlertsEnabled)
        {
            return;
        }

        _ = platformNotificationHandler.RemoveNotificationByTag(BuildNotificationTag(device.Id));
    }

    private void UntrackDevice(PairedDevice device)
    {
        if (settingsChangedHandlers.TryRemove(device.Id, out var handler))
        {
            device.DeviceSettings.PropertyChanged -= handler;
        }

        trackedDevices.TryRemove(device.Id, out _);
        deviceLocks.TryRemove(device.Id, out _);
    }

    private static bool ShouldShowLowBatteryAlert(BatteryState batteryState) =>
        batteryState.BatteryLevel <= LowBatteryThreshold && !batteryState.IsCharging;

    private static string BuildNotificationTag(string deviceId) => $"battery_{deviceId}";
}
