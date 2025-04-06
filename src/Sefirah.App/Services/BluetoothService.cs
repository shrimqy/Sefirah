using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Dialogs;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace Sefirah.App.Services;
public class BluetoothService(ILogger logger) : IBluetoothService
{
    public ObservableCollection<DeviceInformation> BluetoothDevices { get; } = [];

    private readonly DispatcherQueue dispatcher = MainWindow.Instance.DispatcherQueue;

    private DeviceWatcher? deviceWatcher;

    public async Task<DevicePairingResult> PairDeviceAsync(DeviceInformation deviceInformation)
    {
        ArgumentNullException.ThrowIfNull(deviceInformation);

        //if (deviceInformation.Kind != DeviceInformationKind.AssociationEndpoint)
        //{
        //    throw new InvalidOperationException("Does not support this device");
        //}

        deviceInformation.Pairing.Custom.PairingRequested += Custom_PairingRequested;
        DevicePairingResult pairingResult = await deviceInformation.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmPinMatch);
        deviceInformation.Pairing.Custom.PairingRequested -= Custom_PairingRequested;
        return pairingResult;
    }

    private async void Custom_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
    {
        var deferral = args.GetDeferral();
        await dispatcher.EnqueueAsync(async () =>
        {
            var dialog = new BluetoothPairingDialog(args.DeviceInformation.Name, args.Pin)
            {
                XamlRoot = MainWindow.Instance.Content.XamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                args.Accept();
            }
        });
        deferral.Complete();
    }

    public void CreateDeviceWatcher()
    {
        logger.Info("Creating DeviceWatcher for all Bluetooth devices");
        deviceWatcher = DeviceInformation.CreateWatcher(BluetoothDevice.GetDeviceSelector());
        deviceWatcher.Added += DeviceWatcher_Added;
        deviceWatcher.Removed += DeviceWatcher_Removed;
        deviceWatcher.Updated += DeviceWatcher_Updated;
        deviceWatcher.Start();
    }

    private void DeviceWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        dispatcher.EnqueueAsync(() =>
        {
            logger.Info("DeviceWatcher_Updated: {DeviceId}", args.Id);
            var device = BluetoothDevices.FirstOrDefault(d => d.Id == args.Id);
            device?.Update(args);
        });
    }

    private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
    {
        dispatcher.EnqueueAsync(() =>
        {
            logger.Info("DeviceWatcher_Removed: {DeviceId}", args.Id);
            var device = BluetoothDevices.FirstOrDefault(d => d.Id == args.Id); 
            if (device != null)
            {
                BluetoothDevices.Remove(device);
            }
        });
    }

    private void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
    {
        dispatcher.EnqueueAsync(() =>
        {
            logger.Info("DeviceWatcher_Added: {DeviceId}", args.Id);
            BluetoothDevices.Add(args);
        });
    }
}
