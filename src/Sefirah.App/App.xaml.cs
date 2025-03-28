using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;
using Serilog;
using System.IO;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using WinUIEx;

namespace Sefirah.App;


public partial class App : Application
{
    public static bool HandleClosedEvents { get; set; } = true;
    public static TaskCompletionSource<bool>? SplashScreenLoadingTCS { get; private set; }

    public new static App Current
             => (App)Application.Current;

    public App()
    {
        InitializeComponent();
        Log.Logger = GetSerilogLogger();

        // Configure exception handlers
        UnhandledException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.Exception);
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _ = ActivateAsync();

        async Task ActivateAsync()
        {
            // Get AppActivationArgumentsTask
            var activatedEventArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
            var isStartupTask = activatedEventArgs.Data is IStartupTaskActivatedEventArgs;

            // Handle share activation
            if (activatedEventArgs.Kind is ExtendedActivationKind.ShareTarget)
            {
                Debug.WriteLine("Share Target Received");
                await HandleShareTargetActivation(activatedEventArgs.Data as ShareTargetActivatedEventArgs);
            }

            // Configure the DI container
            var host = AppLifecycleHelper.ConfigureHost();
            Ioc.Default.ConfigureServices(host.Services);
            await host.StartAsync();

            // Initialize window but don't show it yet
            MainWindow.Instance.EnsureWindowIsInitialized();

            HookEventsForWindow();

            var window = MainWindow.Instance;
            // Initialize window based on startup options
            if (isStartupTask)
            {
                // Get the startup preference from user settings
                var userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
                var startupOption = userSettingsService.GeneralSettingsService.StartupOption;
                switch (startupOption)
                {
                    case StartupOptions.InTray:
                        // Don't activate
                        break;
                    case StartupOptions.Minimized:
                        window.Activate();
                        window.WindowState = WindowState.Minimized;
                        break;
                    default:
                        window.Activate();
                        window.AppWindow.Show();
                        break;
                }
            }
            else
            {
                window.Activate();
                window.AppWindow.Show();
            }

            // Show Splash Screen only for visible windows
            if (MainWindow.Instance.AppWindow.IsVisible)
            {
                SplashScreenLoadingTCS = new TaskCompletionSource<bool>(); // We are not using this for anything right now
                MainWindow.Instance.ShowSplashScreen();
                await Task.Delay(200);
            }

            // Initialize the main application after splash screen completes
            _ = MainWindow.Instance.InitializeApplicationAsync(activatedEventArgs.Data);

            await AppLifecycleHelper.InitializeAppComponentsAsync();
        }
    }

    private void HookEventsForWindow()
    {
        // Hook events for the window
        MainWindow.Instance.Activated += Window_Activated;

        MainWindow.Instance.Closed += (sender, args) =>
        {
            if (HandleClosedEvents)
            {
                // If HandleClosedEvents is true, we hide the window (tray icon exit logic can change this)
                args.Handled = true;
                MainWindow.Instance.AppWindow.Hide();
            }
        };

    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
            return;
        ApplicationData.Current.LocalSettings.Values["INSTANCE_ACTIVE"] = -Environment.ProcessId;
    }


    /// <summary>
    /// Gets invoked when the application is activated.
    /// </summary>
    public async Task OnActivatedAsync(AppActivationArguments activatedEventArgs)
    {
        var activatedEventArgsData = activatedEventArgs.Data;

        // InitializeApplication accesses UI, needs to be called on UI thread
        await MainWindow.Instance.DispatcherQueue.EnqueueAsync(() => MainWindow.Instance.InitializeApplicationAsync(activatedEventArgsData));
    }

    public async Task HandleShareTargetActivation(ShareTargetActivatedEventArgs? args)
    {
        var shareOperation = args?.ShareOperation;
        if (shareOperation == null) return;
        var fileTransferService = Ioc.Default.GetRequiredService<IFileTransferService>();

        await MainWindow.Instance.DispatcherQueue.EnqueueAsync(async () =>
        {
            await fileTransferService.ProcessShareAsync(shareOperation);
        });
    }

    private static Serilog.ILogger GetSerilogLogger()
    {
        string logFilePath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Log.log");

        var logger = new LoggerConfiguration()
            .MinimumLevel
#if DEBUG
            .Verbose()
#else
			.Debug()
#endif
            .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
            .WriteTo.Debug()
                .CreateLogger();

        return logger;
    }
}