using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Views;
using Sefirah.Views.Onboarding;

namespace Sefirah.ViewModels.Dialogs;
public partial class ConnectionRequestViewModel : ObservableObject
{
    private string deviceName = string.Empty;
    public string DeviceName
    {
        get => deviceName;
        set => SetProperty(ref deviceName, value);
    }

    private string passkey = string.Empty;
    public string Passkey
    {
        get => passkey;
        set => SetProperty(ref passkey, value);
    }

    private readonly Frame _frame;

    public ConnectionRequestViewModel(string deviceName, byte[] hashedKey, Frame frame)
    {
        DeviceName = deviceName;
        var derivedKeyInt = BitConverter.ToInt32(hashedKey, 0);
        derivedKeyInt = Math.Abs(derivedKeyInt) % 1_000_000;
        Passkey = derivedKeyInt.ToString().PadLeft(6, '0');
        _frame = frame;
    }

    public void OnConnectClick()
    {
        // If we're on the onboarding pages, navigate to main page
        if (_frame.Content is not MainPage)
        {
            ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] = true;
            _frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    }
}
