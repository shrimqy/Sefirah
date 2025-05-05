using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Sefirah.App.Views;
using Sefirah.App.Views.Onboarding;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.ViewManagement;
using WinUIEx;

namespace Sefirah.App;

public sealed partial class MainWindow : WindowEx
{
    private static MainWindow? _Instance;
    public static MainWindow Instance => _Instance ??= new();

    private readonly UISettings uiSettings = new();
    public nint WindowHandle { get; }
    public MainWindow()
    {
        InitializeComponent();

        WindowHandle = WindowExtensions.GetWindowHandle(this);
        MinHeight = 416;
        MinWidth = 516;
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
        Title = "Sefirah";
        PersistenceId = "SefirahMainWindow";
        AppWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
        AppWindow.TitleBar.ButtonPressedBackgroundColor = Colors.Transparent;

        var iconPath = uiSettings.GetColorValue(UIColorType.Background) == Colors.Black
            ? "Assets/Icons/SefirahDark.ico"
            : "/Assets/Icons/SefirahLight.ico";

        Debug.WriteLine("app location: " + Package.Current.InstalledLocation.Path + "icon path" + iconPath);
        AppWindow.SetIcon(Path.Combine(Package.Current.InstalledLocation.Path, iconPath));
    }

    public void ShowSplashScreen()
    {
        var rootFrame = EnsureWindowIsInitialized();

        rootFrame.Navigate(typeof(Views.SplashScreen));
    }

    public Task InitializeApplicationAsync(object activatedEventArgs)
    {
        var rootFrame = EnsureWindowIsInitialized();

        // Check if onboarding is complete
        var localSettings = ApplicationData.Current.LocalSettings;
        bool isOnboarding = localSettings.Values["HasCompletedOnboarding"] == null;

        var isFirstLaunch = localSettings.Values["IsFirstLaunch"];
        if (isFirstLaunch == null || true)
        {
            _ = AppLifecycleHelper.HandleStartupTaskAsync(true);
            localSettings.Values["IsFirstLaunch"] = false;
        }

        if (isOnboarding)
        {
            // Navigate to onboarding page and ensure window is visible
            rootFrame.Navigate(typeof(WelcomePage), null, new SuppressNavigationTransitionInfo());
            if (!AppWindow.IsVisible)
            {
                AppWindow.Show();
                Activate();
            }
        }
        else
        {
            // Normal app initialization without forcing window visibility
            switch (activatedEventArgs)
            {
                case ILaunchActivatedEventArgs launchArgs:
                    if (launchArgs != null)
                    {
                        rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
                    }
                    break;
                default:
                    rootFrame.Navigate(typeof(MainPage), null, new SuppressNavigationTransitionInfo());
                    break;
            }
        }

        return Task.CompletedTask;
    }

    public Frame EnsureWindowIsInitialized()
    {
        //  NOTE:
        //  Do not repeat app initialization when the Window already has content,
        //  just ensure that the window is active
        if (Instance.Content is not Frame rootFrame)
        {
            // Create a Frame to act as the navigation context and navigate to the first page
            rootFrame = new() { CacheSize = 1 };
            rootFrame.NavigationFailed += OnNavigationFailed;

            // Place the frame in the current Window
            Instance.Content = rootFrame;
        }

        return rootFrame;
    }

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        => new Exception("Failed to load Page " + e.SourcePageType.FullName);

}