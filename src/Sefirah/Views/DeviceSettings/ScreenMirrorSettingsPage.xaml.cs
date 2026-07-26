using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.ViewModels.Settings;
using Windows.System;

namespace Sefirah.Views.DeviceSettings;

public sealed partial class ScreenMirrorSettingsPage : Page
{
    public DeviceSettingsViewModel ViewModel 
    {
        get => (DeviceSettingsViewModel)DataContext;
        private set => DataContext = value;
    }

    public ScreenMirrorSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is DeviceSettingsViewModel deviceSettingsViewModel)
        {   
            ViewModel = deviceSettingsViewModel;
        }
    }

    private void OpenUnlockSettings(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(UnlockSettingsPage), ViewModel, new DrillInNavigationTransitionInfo());
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter)
        {
            Focus(FocusState.Pointer);
            e.Handled = true;
        }
    }
}
