using Microsoft.Extensions.Hosting;
using Sefirah.App.Data.AppDatabase;
using Sefirah.App.Data.Contracts;
using Sefirah.App.RemoteStorage;
using Sefirah.App.RemoteStorage.Shell;
using Sefirah.App.RemoteStorage.Worker;
using Sefirah.App.Services;
using Sefirah.App.Services.Settings;
using Sefirah.App.Services.Socket;
using Sefirah.App.ViewModels;
using Sefirah.App.ViewModels.Settings;
using Serilog;
using Windows.ApplicationModel;

namespace Sefirah.App.Helpers;


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


    /// <summary>
    /// Initializes the app components.
    /// </summary>
    public static async Task InitializeAppComponentsAsync()
    {
        // Get database context and initialize it
        var dbContext = Ioc.Default.GetRequiredService<DatabaseContext>();
        await dbContext.InitializeAsync();

        var userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
        var generalSettingsService = userSettingsService.GeneralSettingsService;
        var mdnsService = Ioc.Default.GetRequiredService<IMdnsService>();
        var networkService = Ioc.Default.GetRequiredService<INetworkService>();
        var playbackService = Ioc.Default.GetRequiredService<IPlaybackService>();
        var toastNotificationService = Ioc.Default.GetRequiredService<ToastNotificationService>();

        var updateService = Ioc.Default.GetRequiredService<IUpdateService>();
        var bluetoothService = Ioc.Default.GetRequiredService<IBluetoothService>();
        bluetoothService.CreateDeviceWatcher();

        // Start all the required services for startup
        await networkService.StartServerAsync();
        await playbackService.InitializeAsync();
        mdnsService.StartDiscovery();
        toastNotificationService.RegisterNotification();

        var adbService = Ioc.Default.GetRequiredService<IAdbService>();
        await adbService.StartAsync();

        await updateService.CheckForUpdatesAsync();
    }


    /// <summary>
    /// Configures DI (dependency injection) container.
    /// </summary>
    public static IHost ConfigureHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) => services
                // Logging
                .AddSingleton<Sefirah.Common.Utils.ILogger>(new SerilogWrapperLogger(Log.Logger))

                // Settings Services
                .AddSingleton<IUserSettingsService, UserSettingsService>()
                .AddSingleton<IGeneralSettingsService, GeneralSettingsService>(sp => new GeneralSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))
                .AddSingleton<IFeatureSettingsService, FeaturesSettingsService>(sp => new FeaturesSettingsService(((UserSettingsService)sp.GetRequiredService<IUserSettingsService>()).GetSharingContext()))

                // Remote Storage
                .AddSftpRemoteServices()
                .AddCloudSyncWorker()

                // Shell
                .AddCommonClassObjects()
                .AddSingleton<ShellRegistrar>()
                .AddHostedService<ShellWorker>()

                .AddSingleton<SyncProviderWorker>()
                .AddSingleton<ISftpService, SftpService>()

                // Database
                .AddSingleton<DatabaseContext>()
                .AddSingleton<IRemoteAppsRepository, RemoteAppsRepository>()
                .AddSingleton<DeviceRepository>()

                // Services
                .AddSingleton<IBluetoothService, BluetoothService>()
                .AddSingleton<IAdbService, AdbService>()
                .AddSingleton<IUpdateService, AppUpdateService>()
                .AddSingleton<IDeviceManager, DeviceManager>()
                .AddSingleton<IDiscoveryService, DiscoveryService>()
                .AddSingleton<INetworkService, NetworkService>()
                .AddSingleton<IScreenMirrorService, ScreenMirrorService>()
                .AddSingleton(sp => (ITcpServerProvider)sp.GetRequiredService<INetworkService>())
                .AddSingleton(sp => (ISessionManager)sp.GetRequiredService<INetworkService>())
                .AddSingleton<IFileTransferService, FileTransferService>()
                .AddSingleton(sp => (ITcpClientProvider)sp.GetRequiredService<IFileTransferService>())
                .AddSingleton<IMdnsService, MdnsService>()
                .AddSingleton<ISmsHandlerService, SmsHandlerService>()
                .AddSingleton<IClipboardService, ClipboardService>()
                .AddSingleton<IPlaybackService, PlaybackService>()
                .AddSingleton<INotificationService, NotificationService>()
                .AddSingleton<ToastNotificationService>()
                .AddScoped<IMessageHandlerService, MessageHandlerService>()
                .AddSingleton<Func<IMessageHandlerService>>(sp => () => sp.GetRequiredService<IMessageHandlerService>())

                // ViewModels
                .AddSingleton<MainPageViewModel>()
                .AddSingleton<DevicesViewModel>()
                .AddSingleton<MessagesViewModel>()
                .AddSingleton<AppsViewModel>()
                .AddSingleton<FeaturesViewModel>()
                .AddSingleton<CallsViewModel>()
            ).Build();
    }

    /// <summary>
    /// Shows exception on the Debug Output.
    /// </summary>
    public static void HandleAppUnhandledException(Exception? ex)
    {
        Ioc.Default.GetService<Common.Utils.ILogger>()?.Fatal("Unhandled exception", ex);
    }


    public static async Task HandleStartupTaskAsync(bool enable)
    {
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
    }
}
