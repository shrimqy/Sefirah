using Microsoft.UI.Windowing;
using Sefirah.UserControls;
using Sefirah.ViewModels;
using Windows.Graphics;

namespace Sefirah.Views.WindowViews;

public sealed partial class CallWindow : Window
{
    private const int WindowWidth = 350;
    private const int SingleCallWindowHeight = 160;
    private const int DualCallWindowHeight = 230;

    public CallWindowViewModel ViewModel { get; }

    public CallWindow()
    {
        ViewModel = new CallWindowViewModel();
        InitializeComponent();
        Content = new CallView(ViewModel);

        Title = "Call window";
        this.SetWindowIcon();

        var overlapped = (AppWindow.Presenter as OverlappedPresenter) ?? OverlappedPresenter.Create();
        overlapped.IsResizable = false;
        overlapped.IsMaximizable = false;
        overlapped.IsMinimizable = true;
        overlapped.IsAlwaysOnTop = true;
        UpdateWindowSize(false);
#if WINDOWS
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new MicaBackdrop();
#endif
        Closed += OnClosed;
        Activate();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        Closed -= OnClosed;
        ViewModel.Dispose();
    }

    public void UpdateWindowSize(bool hasSecondarySession)
    {
        var height = hasSecondarySession ? DualCallWindowHeight : SingleCallWindowHeight;
        AppWindow.Resize(new SizeInt32 { Width = WindowWidth, Height = height });
    }
}
