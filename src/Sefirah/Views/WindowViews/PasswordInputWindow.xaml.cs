using Microsoft.UI.Windowing;
using Windows.Graphics;
using Windows.System;
using Microsoft.UI.Xaml.Input;


#if WINDOWS
using Sefirah.Platforms.Windows.Interop;
using WinRT.Interop;
#endif

namespace Sefirah.Views.WindowViews;

/// <summary>
/// Small always-on-top password prompt that works even when MainWindow is hidden (e.g. tray).
/// Await <see cref="ShowAsync"/> to get the entered password, or null if cancelled.
/// </summary>
public sealed partial class PasswordInputWindow : Window
{
    private readonly TaskCompletionSource<string?> result = new();
    private bool completed;

    private PasswordInputWindow()
    {
        InitializeComponent();
        this.SetWindowIcon();

        var overlapped = (AppWindow.Presenter as OverlappedPresenter) ?? OverlappedPresenter.Create();
        overlapped.IsResizable = false;
        overlapped.IsMaximizable = false;
        overlapped.IsMinimizable = false;
        overlapped.IsAlwaysOnTop = true;

        AppWindow.Resize(new SizeInt32 { Width = 400, Height = 240 });
        ExtendsContentIntoTitleBar = true;
        Ioc.Default.GetRequiredService<IAppThemeModeService>().ManageAppearance(this);

        Closed += OnClosed;
        Activate();

#if WINDOWS
        InteropHelpers.SetForegroundWindow(WindowNative.GetWindowHandle(this));
#endif

        PasswordBox.Loaded += (_, _) => PasswordBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Shows the password window and completes when the user confirms or cancels.
    /// </summary>
    public static Task<string?> ShowAsync()
    {
        var window = new PasswordInputWindow();
        return window.result.Task;
    }

    private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter)
        {
            e.Handled = true;
            Complete(PasswordBox.Password);
        }
        else if (e.Key is VirtualKey.Escape)
        {
            e.Handled = true;
            Complete(null);
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
        => Complete(PasswordBox.Password);

    private void CancelButton_Click(object sender, RoutedEventArgs e)
        => Complete(null);

    private void OnClosed(object sender, WindowEventArgs args)
        => Complete(null);

    private void Complete(string? password)
    {
        if (completed)
            return;

        completed = true;
        result.TrySetResult(password);

        Closed -= OnClosed;

        try { Close(); } catch { }
    }
}
