using Sefirah.Data.AppDatabase;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Models;
#if WINDOWS
using Sefirah.Platforms.Windows;
using Sefirah.Platforms.Windows.Services;
#else
using Sefirah.Platforms.Desktop;
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
        var clipboardService = Ioc.Default.GetRequiredService<IClipboardService>();
        var networkService = Ioc.Default.GetRequiredService<INetworkService>();
        var notificationService = Ioc.Default.GetRequiredService<INotificationService>();
        var deviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
        var adbService = Ioc.Default.GetRequiredService<IAdbService>();
        var playbackService = Ioc.Default.GetRequiredService<IPlaybackService>();
        var actionService = Ioc.Default.GetRequiredService<IActionService>();

        var updateService = Ioc.Default.GetRequiredService<IUpdateService>();

#if WINDOWS
        var windowsNotificationHandler = Ioc.Default.GetRequiredService<IPlatformNotificationHandler>();
        await windowsNotificationHandler.RegisterForNotifications();
#endif

        notificationService.Initialize();
        clipboardService.Initialize();
        await deviceManager.Initialize();

        await Task.WhenAll(
            networkService.StartServerAsync(),
            playbackService.InitializeAsync(),
            actionService.InitializeAsync(),
            adbService.StartAsync(),
            updateService.CheckForUpdatesAsync()
        ); 

        App.SplashScreenLoadingTCS?.TrySetResult();
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

                // Database and Repositories
                .AddSingleton<DatabaseContext>()
                .AddSingleton<DeviceRepository>()
                .AddSingleton<RemoteAppRepository>()
                .AddSingleton<SmsRepository>()

                // Platform-specific services
#if WINDOWS
                .AddWindowsServices()
#else
                .AddDesktopServices()
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
                .AddSingleton<SmsHandlerService>()

                .AddSingleton<IMessageHandler, MessageHandler>()
                .AddSingleton<Func<IMessageHandler>>(sp => () => sp.GetRequiredService<IMessageHandler>())
                .AddSingleton<IAdbService, AdbService>()
                .AddSingleton<IScreenMirrorService, ScreenMirrorService>()
                .AddSingleton<IFileTransferService, FileTransferService>()

                // ViewModels
                .AddSingleton<MainPageViewModel>()
                .AddSingleton<DevicesViewModel>()
                .AddSingleton<AppsViewModel>()
                .AddSingleton<MessagesViewModel>()
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
