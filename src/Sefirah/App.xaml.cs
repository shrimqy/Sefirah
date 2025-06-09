using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.Windows.AppLifecycle;
using Sefirah.Helpers;
using Sefirah.Models;
using Sefirah.Views;
using Sefirah.Views.Onboarding;
using Uno.Resizetizer;
using Windows.ApplicationModel.Activation;
using LaunchActivatedEventArgs = Microsoft.UI.Xaml.LaunchActivatedEventArgs;

namespace Sefirah;
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }
    protected IHost? Host { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var builder = this.ConfigureApp(args);
            
        MainWindow = builder.Window;

#if DEBUG
        MainWindow.UseStudio();
#endif
        MainWindow.SetWindowIcon();

        Host = builder.Build();
        Ioc.Default.ConfigureServices(Host.Services);
        Host.StartAsync();

        EnsureWindowIsInitialized();

        InitializeApplicationAsync(args);
        _ = AppLifeCycleHelper.InitializeAppComponentsAsync();
    }

    public Frame EnsureWindowIsInitialized()
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

    public Task InitializeApplicationAsync(object activatedEventArgs)
    {
        var rootFrame = EnsureWindowIsInitialized();

        MainWindow!.Activate();
        MainWindow.AppWindow.Show();

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

        return Task.CompletedTask;
    }

    public void ShowSplashScreen()
    {
        var rootFrame = EnsureWindowIsInitialized();

        rootFrame.Navigate(typeof(Views.SplashScreen));
    }

    /// <summary>
    /// Gets invoked when the application is activated.
    /// </summary>
    public async Task OnActivatedAsync(AppActivationArguments activatedEventArgs)
    {
        var activatedEventArgsData = activatedEventArgs.Data;

        // InitializeApplication accesses UI, needs to be called on UI thread
        //await MainWindow!.DispatcherQueue.EnqueueAsync(() => InitializeApplicationAsync(activatedEventArgsData));
    }

#if WINDOWS
    private void HandleToastActivation()
    {
        // Check if this is a toast activation
        var activatedArgs = Microsoft.Windows.AppLifecycle.AppInstance.GetCurrent().GetActivatedEventArgs();
        if (activatedArgs?.Kind == Microsoft.Windows.AppLifecycle.ExtendedActivationKind.ToastNotification)
        {
            // The app was launched via toast notification
            // The ToastNotificationService will handle the actual notification processing
        }
    }
#endif

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        => new Exception("Failed to load Page " + e.SourcePageType.FullName);
}
