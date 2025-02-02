using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using static Sefirah.App.Constants;

namespace Sefirah.App.Views.Onboarding;

public sealed partial class WelcomePage : Page
{
    public WelcomePage()
    {
        this.InitializeComponent();
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
