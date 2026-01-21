using CommunityToolkit.WinUI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.ViewModels.Settings;
using Sefirah.Views.DevicePreferences;
using Windows.Graphics;
using Rect = Windows.Foundation.Rect;

namespace Sefirah.Views;
public sealed partial class DeviceSettingsWindow : Window
{
    public PairedDevice Device { get; }

    private readonly IUserSettingsService userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();

    public DeviceSettingsWindow(PairedDevice device)
    {
        Device = device;
        var viewModel = new DeviceSettingsViewModel(device);
        
        InitializeComponent();
        Title = device.Name;
        this.SetWindowIcon();
        OverlappedPresenter overlappedPresenter = (AppWindow.Presenter as OverlappedPresenter) ?? OverlappedPresenter.Create();
        overlappedPresenter.IsMaximizable = false;
        overlappedPresenter.IsMinimizable = false;

        AppWindow.Resize(new SizeInt32 { Width = 600, Height = 900 });
#if WINDOWS
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
        
        BackButton.Loaded += (s, e) => SetRegionsForCustomTitleBar();
#endif
        var rootFrame = EnsureWindowIsInitialized();
        rootFrame.Navigate(typeof(DeviceSettingsPage), viewModel);
        InitializeThemeService();

        AppWindow.Closing += (s, e) => App.RemoveDeviceSettingsWindow(Device.Id);
    }

    private void InitializeThemeService()
    {
        // Get the user settings service if available
        userSettingsService.GeneralSettingsService.ThemeChanged += AppThemeChanged;
        userSettingsService.GeneralSettingsService.ApplyTheme(this, AppWindow.TitleBar, userSettingsService.GeneralSettingsService.Theme);
    }

    private async void AppThemeChanged(object? sender, EventArgs e)
    {
        if (AppWindow is null) return;

        await DispatcherQueue.EnqueueAsync(() =>
        {
            userSettingsService.GeneralSettingsService.ApplyTheme(this, AppWindow.TitleBar, userSettingsService.GeneralSettingsService.Theme, false);
        });
    }


    public Frame EnsureWindowIsInitialized()
    {
        //  NOTE:
        //  Do not repeat app initialization when the Window already has content,
        //  just ensure that the window is active
        if (this.Content is not Grid rootGrid)
        {
            // The window content has already been set up in XAML
            rootGrid = (Grid)Content!;
        }

        var rootFrame = (Frame)rootGrid.FindName("RootFrame");
        rootFrame.NavigationFailed += OnNavigationFailed;

        return rootFrame;
    }

    private void SetRegionsForCustomTitleBar()
    {
#if WINDOWS
        // Specify the interactive regions of the title bar.
        double scaleAdjustment = BackButton.XamlRoot.RasterizationScale;

        // Get the rectangle around the back button
        GeneralTransform transform = BackButton.TransformToVisual(null);
        Rect bounds = transform.TransformBounds(new Rect(0, 0,
                                                         BackButton.ActualWidth,
                                                         BackButton.ActualHeight));
        Windows.Graphics.RectInt32 backButtonRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new Windows.Graphics.RectInt32[] { backButtonRect };

        InputNonClientPointerSource nonClientInputSrc =
            InputNonClientPointerSource.GetForWindowId(AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);
#endif
    }

#if WINDOWS
    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale)
        );
    }
#endif

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        => new Exception("Failed to load Page " + e.SourcePageType.FullName);

    private void TitleBar_BackRequested(object sender, RoutedEventArgs e)
    {
        if (RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }
}
