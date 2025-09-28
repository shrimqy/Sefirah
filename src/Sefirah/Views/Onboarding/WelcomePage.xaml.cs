using Windows.System;
using static Sefirah.Constants;

namespace Sefirah.Views.Onboarding;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        InitializeComponent();
    }

    private async void OnGitHubButton_Click(object sender, RoutedEventArgs e)
    {
        var uri = new Uri(ExternalUrl.AndroidGitHubRepoUrl);
        await Launcher.LaunchUriAsync(uri);
    }

    private void OnGetStartedButton_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(SyncPage));
    }
}
