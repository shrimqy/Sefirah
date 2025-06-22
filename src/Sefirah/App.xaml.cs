using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.AppLifecycle;
using Sefirah.Helpers;
using Sefirah.Views;
using Sefirah.Views.Onboarding;
using Uno.Resizetizer;
using Windows.ApplicationModel.Activation;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;
using H.NotifyIcon;
using Sefirah.Data.Contracts;
using System.Runtime.InteropServices;



#if WINDOWS
using Sefirah.Platforms.Windows.Helpers;
#endif

namespace Sefirah;
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // Configure exception handlers
        UnhandledException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (sender, e) => AppLifecycleHelper.HandleAppUnhandledException(e.Exception);
    }
    public static bool HandleClosedEvents { get; set; } = true;
    public static Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.ConfigureApp(args);
            
        MainWindow = builder.Window;
#if WINDOWS
        MainWindow.ExtendsContentIntoTitleBar = true;
        MainWindow.SystemBackdrop = new MicaBackdrop();
#endif
        MainWindow.AppWindow.Title = "Sefirah";
#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();
        Ioc.Default.ConfigureServices(Host.Services);
        Host.StartAsync();

#if WINDOWS
        HookEventsForWindow();
#endif
        var rootFrame = EnsureWindowIsInitialized();

        if (rootFrame is null)
            return;

        switch (args)
        {
            default:
                bool isOnboarding = ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] == null;
                if (isOnboarding)
                {
                    // Navigate to onboarding page and ensure window is visible
                    rootFrame.Navigate(typeof(WelcomePage), null, new SuppressNavigationTransitionInfo());
                }
                else
                {
                    // Navigate to main page and ensure window is visible
                    rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
                }
                break;
        }

        MainWindow!.Activate();
        MainWindow.AppWindow.Show();

        _ = AppLifecycleHelper.InitializeAppComponentsAsync();
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
#endif

    public void ShowSplashScreen()
    {
        var rootFrame = EnsureWindowIsInitialized();
        if (rootFrame is null)
            return; 

        rootFrame.Navigate(typeof(Views.SplashScreen));
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

    private void HookEventsForWindow()
    {
        MainWindow!.Activated += Window_Activated;
        MainWindow.Closed += Window_Closed;
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        if (HandleClosedEvents)
        {
            // If HandleClosedEvents is true, we hide the window (tray icon exit logic can change this)
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
