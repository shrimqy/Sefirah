using Sefirah.Platforms.Desktop.Bluetooth;
using Sefirah.Platforms.Desktop.Features;
using Sefirah.Platforms.Desktop.Services;

namespace Sefirah.Platforms.Desktop;

/// <summary>
/// Extension methods for registering Desktop-specific services and features.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPlatformServices(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformNotificationHandler, NotificationHandler>();
        services.AddFeature<IMediaFeature, MediaFeature>();
        services.AddFeature<IBatteryFeature, BatteryFeature>();
        services.AddFeature<ISftpFeature, SftpFeature>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IAppShortcutService, AppShortcutService>();
        services.AddSingleton<IPhoneLineService, PhoneLineService>();
        services.AddSingleton<IBluetoothPairingService, BluetoothPairingService>();
        services.AddSingleton<BluetoothPairingService>(sp => (BluetoothPairingService)sp.GetRequiredService<IBluetoothPairingService>());
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        return services;
    }
}
