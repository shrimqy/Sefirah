using Microsoft.UI.Xaml.Input;
using Sefirah.ViewModels.Settings;
using Windows.System;

namespace Sefirah.Views.DeviceSettings;

public sealed partial class UnlockSettingsPage : Page
{
    public DeviceSettingsViewModel ViewModel
    {
        get => (DeviceSettingsViewModel)DataContext;
        private set => DataContext = value;
    }

    public UnlockSettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is DeviceSettingsViewModel viewModel)
            ViewModel = viewModel;
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Enter)
        {
            Focus(FocusState.Pointer);
            e.Handled = true;
        }

        // Defer so the binding has applied the key before we persist.
        DispatcherQueue.TryEnqueue(() => ViewModel.SaveUnlockCommands());
    }

    private void OnDelayValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
        DispatcherQueue.TryEnqueue(() => ViewModel.SaveUnlockCommands());
}
