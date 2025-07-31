using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.AppLifecycle;
using Sefirah.Helpers;
using Sefirah.Views;
using Sefirah.Views.Onboarding;
using Windows.ApplicationModel.Activation;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;
using H.NotifyIcon;
using Sefirah.Data.Contracts;
using System.Runtime.InteropServices;
using Uno.Resizetizer;
using Sefirah.Extensions;
using Sefirah.Data.Enums;
using Microsoft.UI.Windowing;
using WinRT.Interop;

#if WINDOWS
using Sefirah.Platforms.Windows.Helpers;
#endif

namespace Sefirah;
public partial class App : Application
{
    public static TaskCompletionSource? SplashScreenLoadingTCS { get; private set; }
    public static bool HandleClosedEvents { get; set; } = true;
    public static nint WindowHandle { get; private set; }
    public static Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    public App()
    {
        InitializeComponent();
        // Configure exception handlers
        UnhandledException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.Exception);
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = ActivateAsync();

        async Task ActivateAsync()
        {
            var builder = this.ConfigureApp(args);
            MainWindow = builder.Window;
            MainWindow.AppWindow.Title = "Sefirah";
            MainWindow.SetWindowIcon();
#if WINDOWS
            WindowHandle = WindowNative.GetWindowHandle(MainWindow);
            MainWindow.ExtendsContentIntoTitleBar = true;
            MainWindow.SystemBackdrop = new MicaBackdrop();
#endif
#if DEBUG
            MainWindow.UseStudio();
#endif
            Host = builder.Build();
            Ioc.Default.ConfigureServices(Host.Services);
            await Host.StartAsync();

            bool isStartupTask = false;
#if WINDOWS
            var appActivationArguments = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
            isStartupTask = appActivationArguments.Data is IStartupTaskActivatedEventArgs;

            HookEventsForWindow();
            bool isStartupRegistered = ApplicationData.Current.LocalSettings.Values["isStartupRegistered"] == null;
            if (isStartupRegistered)
            {
                await AppLifecycleHelper.HandleStartupTaskAsync(true);
                ApplicationData.Current.LocalSettings.Values["isStartupRegistered"] = true;
            }
#endif
            var rootFrame = EnsureWindowIsInitialized();
            if (rootFrame is null)
                return;

            if (isStartupTask)
            {
                var userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
                var startupOption = userSettingsService.GeneralSettingsService.StartupOption;
                switch (startupOption)
                {
                    case StartupOptions.InTray:
                        // Don't activate or show the window
                        break;
                    case StartupOptions.Minimized:
                        // Need to show the window first, then minimize it
                        MainWindow.Activate();
                        await Task.Delay(200);
                        OverlappedPresenter overlappedPresenter = (MainWindow.AppWindow.Presenter as OverlappedPresenter) ?? OverlappedPresenter.Create();
                        if (overlappedPresenter.IsMinimizable)
                        {
                            overlappedPresenter.Minimize();
                        }
                        break;
                    default:
                        MainWindow.Activate();
                        MainWindow.AppWindow.Show();
                        break;
                };
            }
            else 
            {
                MainWindow!.Activate();
                // Wait for the Window to initialize
                await Task.Delay(10);
                MainWindow.AppWindow.Show();
            }

            rootFrame.Navigate(typeof(Views.SplashScreen));

            SplashScreenLoadingTCS = new TaskCompletionSource();
            await SplashScreenLoadingTCS!.Task.WithTimeoutAsync(TimeSpan.FromMilliseconds(500));
            SplashScreenLoadingTCS = null;

            await AppLifecycleHelper.InitializeAppComponentsAsync();

            bool isOnboarding = ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] == null;
            if (isOnboarding)
            {
                // Navigate to onboarding page
                rootFrame.Navigate(typeof(WelcomePage), null, new SuppressNavigationTransitionInfo());
            }
            else
            {
                // Navigate to main page
                rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
            }
        }
    }

    public Frame? EnsureWindowIsInitialized()
    {
        try
    {
        //  NOTE:
        //  Do not repeat app initialization when the Window already has content,
        //  just ensure that the window is active
        if (MainWindow?.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new() { CacheSize = 1 };
            rootFrame.NavigationFailed += OnNavigationFailed;

            // Place the frame in the current Window
            MainWindow!.Content = rootFrame;
        }

        return rootFrame;
    }

        catch (COMException)
        {
            return null;
        }
    }


#if WINDOWS

    /// <summary>
    /// Gets invoked when the application is activated.
    /// </summary>
    public async Task OnActivatedAsync(AppActivationArguments activatedEventArgs)
    {
        // InitializeApplication accesses UI, needs to be called on UI thread
        await MainWindow!.DispatcherQueue.EnqueueAsync(() => InitializeApplicationAsync(activatedEventArgs));
    }

    public async Task InitializeApplicationAsync(AppActivationArguments activatedEventArgs)
    {
        switch (activatedEventArgs.Data)
        {
            case ShareTargetActivatedEventArgs:
                // Handle share target activation
                await HandleShareTargetActivation(activatedEventArgs.Data as ShareTargetActivatedEventArgs);
                break;

            default:
                MainWindow!.AppWindow.Show();
                MainWindow!.Activate();
                break;
        }
    }

    private void HookEventsForWindow()
    {
        MainWindow!.Activated += Window_Activated;
        MainWindow.Closed += Window_Closed;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (HandleClosedEvents)
        {
            args.Handled = true;
            MainWindow!.Hide();
        }
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
            return;

            ApplicationData.Current.LocalSettings.Values["INSTANCE_ACTIVE"] = -Environment.ProcessId;
    }

    public async Task HandleShareTargetActivation(ShareTargetActivatedEventArgs? args)
    {
        var shareOperation = args?.ShareOperation;
        if (shareOperation == null) return;
        var fileTransferService = Ioc.Default.GetRequiredService<IFileTransferService>();

        await MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
        {
            await fileTransferService.ProcessShareAsync(shareOperation);
        });
    }
#endif

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        => new Exception("Failed to load Page " + e.SourcePageType.FullName);
}
