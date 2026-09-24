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
        ShowLocalAddresses();
    }

    /// <summary>
    /// Spelled out so the address can be typed on the other device when discovery cannot reach it.
    /// </summary>
    private void ShowLocalAddresses()
    {
        var addresses = NetworkHelper.GetAllValidAddresses().Select(a => a.Address.ToString()).ToList();
        LocalAddressText.Text = addresses.Count > 0
            ? string.Format("ThisDeviceAddress".GetLocalizedResource(), string.Join(", ", addresses))
            : "ConnectManuallyDescription".GetLocalizedResource();
    }

    private void ConnectByAddress_Click(object sender, RoutedEventArgs e)
    {
        var input = ManualAddressBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        var address = input;
        var port = 5150;

        var separator = input.LastIndexOf(':');
        if (separator > 0 && int.TryParse(input[(separator + 1)..], out var parsedPort))
        {
            address = input[..separator];
            port = parsedPort;
        }

        if (!System.Net.IPAddress.TryParse(address, out _))
        {
            ManualAddressStatus.Text = "InvalidAddress".GetLocalizedResource();
            return;
        }

        // The id only guards against duplicate attempts; the device announces its real one at handshake
        ManualAddressStatus.Text = string.Format("ConnectingToAddress".GetLocalizedResource(), address, port);
        SessionManager.Connect(address, address, port);
    }

    private void SetupBreadcrumb()
    {
        BreadcrumbBar.ItemsSource = new ObservableCollection<BreadcrumbBarItemModel>
        {
            new("Devices.Title".GetLocalizedResource(), typeof(DevicesPage)),
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


    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is DiscoveredDevice device)
        {
            SessionManager.Pair(device);
        }
    }

    private async void QrCodeButton_Click(object sender, RoutedEventArgs e)
    {
        var bitmapImage = await DiscoveryService.GenerateQrCodeAsync();
        
        if (bitmapImage is null)
        {
            QrCodeImage.Source = null;
            return;
        }

        QrCodeImage.Source = bitmapImage;
        QrCodeStatusText.Text = $"Scan this QR code to connect";
    }

}

