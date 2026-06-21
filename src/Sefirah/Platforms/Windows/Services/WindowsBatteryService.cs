using Sefirah.Data.Models;
using Windows.Devices.Power;
using BatteryStatus = Windows.System.Power.BatteryStatus;

namespace Sefirah.Platforms.Windows.Services;

public class WindowsBatteryService(
    ILogger logger,
    ISessionManager sessionManager) : IBatteryService
{
    private BatteryState? lastBatteryState;

    public Task InitializeAsync()
    {
        if (!IsBatteryPresent()) return Task.CompletedTask;

        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        Battery.AggregateBattery.ReportUpdated += OnBatteryReportUpdated;
        BroadcastBatteryStatus();

        return Task.CompletedTask;
    }

    public void SendBatteryStatus(PairedDevice device)
    {
        if (!device.IsConnected) return;

        var batteryState = GetBatteryState();
        if (batteryState is null) return;

        device.SendMessage(batteryState);
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        SendBatteryStatus(device);
    }

    private void OnBatteryReportUpdated(Battery sender, object args)
    {
        BroadcastBatteryStatus();
    }

    private void BroadcastBatteryStatus()
    {
        var batteryState = GetBatteryState();
        if (batteryState is null || !HasBatteryStateChanged(batteryState)) return;

        lastBatteryState = batteryState;
        sessionManager.BroadcastMessage(batteryState);
    }

    private bool HasBatteryStateChanged(BatteryState batteryState)
    {
        return lastBatteryState is null ||
               lastBatteryState.BatteryLevel != batteryState.BatteryLevel ||
               lastBatteryState.IsCharging != batteryState.IsCharging;
    }

    private static bool IsBatteryPresent()
    {
        return Battery.AggregateBattery.GetReport().Status is not BatteryStatus.NotPresent;
    }

    private BatteryState? GetBatteryState()
    {
        try
        {
            var report = Battery.AggregateBattery.GetReport();
            if (report.Status is BatteryStatus.NotPresent)
            {
                return null;
            }

            var remainingCapacity = report.RemainingCapacityInMilliwattHours;
            var fullChargeCapacity = report.FullChargeCapacityInMilliwattHours;
            if (!remainingCapacity.HasValue || !fullChargeCapacity.HasValue || fullChargeCapacity.Value <= 0)
            {
                return null;
            }

            var batteryLevel = (int)Math.Round(remainingCapacity.Value * 100d / fullChargeCapacity.Value);
            return new BatteryState
            {
                BatteryLevel = Math.Clamp(batteryLevel, 0, 100),
                IsCharging = report.Status is BatteryStatus.Charging
            };
        }
        catch (Exception ex)
        {
            logger.Error("Failed to read battery status", ex);
            return null;
        }
    }
}
