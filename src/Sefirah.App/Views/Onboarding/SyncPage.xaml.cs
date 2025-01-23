using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.App.ViewModels.Settings;
using Sefirah.App.ViewModels;
using Windows.Storage;

namespace Sefirah.App.Views.Onboarding;

public sealed partial class SyncPage : Page
{
    public DevicesViewModel ViewModel { get; }
    public SyncPage()
    {
        this.InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<DevicesViewModel>();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        // Mark onboarding as completed
        ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] = true;

        // Navigate to main page
        Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
    }
}
