using Sefirah.Data.AppDatabase;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Models;
#if WINDOWS
using Sefirah.Platforms.Windows;
#endif
using Sefirah.Services;
using Sefirah.Services.Settings;
using Sefirah.Services.Socket;
using Sefirah.ViewModels;
using Sefirah.ViewModels.Settings;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sefirah.Helpers;

/// <summary>
/// Provides static helper to manage app lifecycle.
/// </summary>
public static class AppLifecycleHelper
{
    /// <summary>
    /// Gets application package version.
    /// </summary>
    public static Version AppVersion { get; } =
        new(Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);

    public static async Task InitializeAppComponentsAsync()
    {
        var networkService = Ioc.Default.GetRequiredService<INetworkService>();
        var notificationService = Ioc.Default.GetRequiredService<INotificationService>();
        var deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
        var adbService = Ioc.Default.GetRequiredService<IAdbService>();

#if WINDOWS
        var toastNotificationService = Ioc.Default.GetRequiredService<ToastNotificationService>();
        await toastNotificationService.RegisterNotificationAsync();
#endif

        notificationService.Initialize();
        await Task.WhenAll(
            deviceManager.Initialize(),
            networkService.StartServerAsync(),
            adbService.StartAsync()
        );
    } 

    public static IApplicationBuilder ConfigureApp(this App app, LaunchActivatedEventArgs args)
    {
        return app.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Debug :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                }, enableUnoLogging: true)
                .UseSerilog(
                    consoleLoggingEnabled: true,
                    fileLoggingEnabled: true,
                    configureLogger: config =>
                    {
                        config.WriteTo.File(
                            Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs", "Log_.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 7
                        );
                    }
                )
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                .UseLocalization()
                .ConfigureServices((context, services) => services

                .AddSingleton<ILogger>(sp => sp.GetRequiredService<ILogger<App>>())

                // Settings Services
                .AddSingleton<IUserSettingsService, UserSettingsService>()
                .AddSingleton<IGeneralSettingsService, GeneralSettingsService>(sp => new GeneralSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
                .AddSingleton<IFeatureSettingsService, FeaturesSettingsService>(sp => new FeaturesSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))

                // Database and Repositories
                .AddSingleton<DatabaseContext>()
                .AddSingleton<DeviceRepository>()
                .AddSingleton<RemoteAppRepository>()

                // Platform-specific services
#if WINDOWS
                .AddWindowsServices()
#endif
                // Services
                .AddSingleton<IDeviceManager, DeviceManager>()
                .AddSingleton(sp => (ITcpServerProvider)sp.GetRequiredService<INetworkService>())
                .AddSingleton(sp => (ISessionManager)sp.GetRequiredService<INetworkService>())
                .AddSingleton<IMdnsService, MdnsService>()
                .AddSingleton<IDiscoveryService, DiscoveryService>()
                .AddSingleton<INetworkService, NetworkService>()

                .AddSingleton<INotificationService, NotificationService>()
                .AddSingleton<IClipboardService, ClipboardService>()

                .AddSingleton<IMessageHandler, MessageHandler>()
                .AddSingleton<Func<IMessageHandler>>(sp => () => sp.GetRequiredService<IMessageHandler>())
                .AddSingleton<IAdbService, AdbService>()
                .AddSingleton<IScreenMirrorService, ScreenMirrorService>()

                // ViewModels
                .AddSingleton<MainPageViewModel>()
                .AddSingleton<DevicesViewModel>()
                .AddSingleton<AppsViewModel>()
                )
            );
    }

    /// <summary>
    /// Shows exception on the Debug Output.
    /// </summary>
    public static void HandleAppUnhandledException(Exception? ex)
    {
        Ioc.Default.GetService<ILogger>()?.LogCritical("Unhandled exception {ex}", ex);
    }

    public static async Task HandleStartupTaskAsync(bool enable)
    {
#if WINDOWS
        var startupTask = await StartupTask.GetAsync("8B5D3E3F-9B69-4E8A-A9F7-BFCA793B9AF0");

        if (enable)
        {
            if (startupTask.State == StartupTaskState.Disabled)
                await startupTask.RequestEnableAsync();
        }
        else
        {
            if (startupTask.State == StartupTaskState.Enabled)
                startupTask.Disable();
        }
#endif
    }
}
