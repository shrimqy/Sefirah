using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Views;

namespace Sefirah.ViewModels.Dialogs;
public partial class ConnectionRequestViewModel : ObservableObject
{
    private string deviceName = string.Empty;
    public string DeviceName
    {
        get => deviceName;
        set => SetProperty(ref deviceName, value);
    }

    private string verificationKey = string.Empty;
    public string VerificationKey
    {
        get => verificationKey;
        set => SetProperty(ref verificationKey, value);
    }

    private readonly Frame frame;

    public ConnectionRequestViewModel(string deviceName, string verificationKey, Frame frame)
    {
        DeviceName = deviceName;
        VerificationKey = verificationKey;
        this.frame = frame;
    }

    public void OnConnectClick()
    {
        // If we're on the onboarding pages, navigate to main page
        if (frame.Content is not MainPage)
        {
            ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] = true;
            frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
        }
    }
}
