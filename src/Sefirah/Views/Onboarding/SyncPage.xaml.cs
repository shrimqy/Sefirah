using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.ViewModels.Settings;

namespace Sefirah.Views.Onboarding;

public sealed partial class SyncPage : Page
{
    public DevicesViewModel ViewModel { get; }
    public SyncPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<DevicesViewModel>();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        // Mark onboarding as completed
        ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] = true;
        Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
    }
}
