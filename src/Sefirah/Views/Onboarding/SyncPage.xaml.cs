using Microsoft.UI.Xaml.Media.Animation;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Utils.Serialization;
using Sefirah.ViewModels.Settings;

namespace Sefirah.Views.Onboarding;

public sealed partial class SyncPage : Page
{
    public DevicesViewModel ViewModel { get; }

    private readonly ISessionManager SessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
    private readonly IDiscoveryService DiscoveryService = Ioc.Default.GetRequiredService<IDiscoveryService>();

    public SyncPage()
    {
        InitializeComponent();
        ViewModel = Ioc.Default.GetRequiredService<DevicesViewModel>();
        Loaded += SyncPage_Loaded;
    }

    private async void SyncPage_Loaded(object sender, RoutedEventArgs e)
    {
        await GenerateQrCodeAsync();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        // Mark onboarding as completed
        ApplicationData.Current.LocalSettings.Values["HasCompletedOnboarding"] = true;
        Frame.Navigate(typeof(MainPage), null, new DrillInNavigationTransitionInfo());
    }

    private async Task GenerateQrCodeAsync()
    {
        try
        {
            var bitmapImage = await DiscoveryService.GenerateQrCodeAsync();
            if (bitmapImage is not null)
            {
                QrCodeImage.Source = bitmapImage;
                QrCodeImage.Visibility = Visibility.Visible;    
            }
            else
            {
                QrCodeImage.Source = null;
                QrCodeImage.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            QrCodeImage.Source = null;
            QrCodeImage.Visibility = Visibility.Collapsed;
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DiscoveredDevice device)
        {
            try
            {
                await SessionManager.Pair(device);
            }
            catch (Exception ex)
            {
            }
        }
    }
}
