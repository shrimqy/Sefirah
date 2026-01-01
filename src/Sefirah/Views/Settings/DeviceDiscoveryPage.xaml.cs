using Sefirah.Data.Contracts;
using Sefirah.Data.Items;
using Sefirah.Data.Models;
using Sefirah.Helpers;
using Sefirah.Utils.Serialization;

namespace Sefirah.Views.Settings;

public sealed partial class DeviceDiscoveryPage : Page
{
    private readonly ISessionManager SessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
    private readonly IDiscoveryService DiscoveryService = Ioc.Default.GetRequiredService<IDiscoveryService>();

    public DeviceDiscoveryPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
    }

    private void SetupBreadcrumb()
    {
        BreadcrumbBar.ItemsSource = new ObservableCollection<BreadcrumbBarItemModel>
        {
            new("Devices".GetLocalizedResource(), typeof(DevicesPage)),
            new("AvailableDevices/Title".GetLocalizedResource(), typeof(DeviceDiscoveryPage))
        };
        BreadcrumbBar.ItemClicked += BreadcrumbBar_ItemClicked;
    }

    private void BreadcrumbBar_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        var items = BreadcrumbBar.ItemsSource as ObservableCollection<BreadcrumbBarItemModel>;
        var clickedItem = items?[args.Index];
        
        if (clickedItem?.PageType is not null && clickedItem.PageType != typeof(DeviceDiscoveryPage))
        {
            // Navigate back to devices page
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DiscoveryService.StartDiscoveryAsync();
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DiscoveredDevice device)
        {
            try
            {
                await SessionManager.Pair(device);
            }
            catch (Exception)
            {
            }
        }
    }

}

