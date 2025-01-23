using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml;
using Sefirah.App.Views.Onboarding;
using Windows.Storage;
using Sefirah.App.Views;
using WinRT;
using Sefirah.App.Data.Enums;

namespace Sefirah.App.ViewModels.Dialogs;

public partial class ConnectionRequestViewModel : ObservableObject
{
    [ObservableProperty]
    private string deviceName;

    [ObservableProperty]
    private string passkey;

    private readonly Frame _frame;

    public ConnectionRequestViewModel(string deviceName, byte[] hashedKey, Frame frame)
    {
        DeviceName = deviceName;
        var derivedKeyInt = BitConverter.ToInt32(hashedKey, 0);
        derivedKeyInt = Math.Abs(derivedKeyInt) % 1_000_000;
        Passkey = derivedKeyInt.ToString().PadLeft(6, '0');
        _frame = frame;
    }

    public void OnConnectClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // If we're on the onboarding page, navigate to main page
        if (_frame.Content is OnboardingDevicePage)
        {
            ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] = true;
            _frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    }
}
