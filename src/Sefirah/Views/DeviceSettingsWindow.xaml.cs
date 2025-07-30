using CommunityToolkit.WinUI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Views.DevicePreferences;
using Rect = Windows.Foundation.Rect;

namespace Sefirah.Views;
public sealed partial class DeviceSettingsWindow : Window
{
    public PairedDevice Device { get; }
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
    public DeviceSettingsWindow(PairedDevice device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        
        this.InitializeComponent();
        Title = device.Name;
        this.SetWindowIcon();
        OverlappedPresenter overlappedPresenter = (AppWindow.Presenter as OverlappedPresenter) ?? OverlappedPresenter.Create();
        overlappedPresenter.IsMaximizable = false;
        overlappedPresenter.IsMinimizable = false;

        AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 600, Height = 900 });
#if WINDOWS
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
        
        // Setup for back button regions
        BackButton.Loaded += (s, e) => SetRegionsForCustomTitleBar();
#endif
        var rootFrame = EnsureWindowIsInitialized();
        rootFrame.Navigate(typeof(DeviceSettingsPage), device);
        InitializeThemeService();
    }

    private void InitializeThemeService()
    {
        // Get the user settings service if available
        UserSettingsService.GeneralSettingsService.ThemeChanged += AppThemeChanged;
        UserSettingsService.GeneralSettingsService.ApplyTheme(this, AppWindow.TitleBar, UserSettingsService.GeneralSettingsService.Theme);
    }

    private async void AppThemeChanged(object? sender, EventArgs e)
    {
        if (AppWindow == null) return;

        await DispatcherQueue.EnqueueAsync(() =>
        {
            UserSettingsService?.GeneralSettingsService.ApplyTheme(this, AppWindow.TitleBar, UserSettingsService.GeneralSettingsService.Theme, false);
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
    private Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
    {
        return new Windows.Graphics.RectInt32(
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
