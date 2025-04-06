using CommunityToolkit.WinUI;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Utils;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Calls;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Media.Capture;
using Windows.Media.Devices;

namespace Sefirah.App.ViewModels;

public partial class CallsViewModel : BaseViewModel
{
    private IBluetoothService BluetoothService { get; } = Ioc.Default.GetRequiredService<IBluetoothService>();

    public ObservableCollection<DeviceInformation> BluetoothDevices => BluetoothService.BluetoothDevices;

    private DeviceInformation? selectedDevice;
    public DeviceInformation? SelectedDevice
    {
        get => selectedDevice;
        set => SetProperty(ref selectedDevice, value);
    }

    public CallsViewModel()
    {
    }

    public async Task RegisterApp()
    {
        await dispatcher.EnqueueAsync(async ()=>
        {
            var featureId = "com.microsoft.windows.applicationmodel.phonelinetransportdevice_v1";
            var token = FeatureTokenGenerator.GenerateTokenFromFeatureId(featureId);
            var attestation = FeatureTokenGenerator.GenerateAttestation(featureId);
            var accessResult = LimitedAccessFeatures.TryUnlockFeature(featureId, token, attestation);
            if (accessResult != null)
            {
                logger.Info($"{featureId}. result: {accessResult.Status}");
            }
            if (SelectedDevice == null)
            {
                logger.Error("No device selected");
                return;
            }
            var currentDevice = SelectedDevice;
            if (!currentDevice.Pairing.IsPaired)
            {
                logger.Error($"Device {currentDevice.Name} is not paired.");
                var pairingResult = await BluetoothService.PairDeviceAsync(currentDevice);

                if (pairingResult.Status != DevicePairingResultStatus.Paired)
                {
                    logger.Error($"Failed to pair device {currentDevice.Name}. Error: {pairingResult.Status}");
                }
                logger.Info("Device {DeviceName} paired successfully.", currentDevice.Name);
            }
            try
            {
                BluetoothDevice bluetoothDevice = await BluetoothDevice.FromIdAsync(currentDevice.Id);
                await Task.Delay(3000);
                PhoneLineTransportDevice? pltDevice = await PhoneLineTransportHelper.GetPhoneLineTransportFromBluetoothDevice(bluetoothDevice);

                if (pltDevice == null)
                {
                    logger.Warn("pltDevice was null");
                    return;
                }
                DeviceAccessStatus accessStatus = await pltDevice.RequestAccessAsync();
                if (accessStatus == DeviceAccessStatus.Allowed)
                {
                    pltDevice.RegisterApp();
                    var isRegistered = pltDevice.IsRegistered();
                    if (!isRegistered)
                    {
                        logger.Warn($"Failed to register {currentDevice.Name}");
                        return;
                    }
                    var isConnected = await pltDevice.ConnectAsync();
                    if (!isConnected)
                    {
                        logger.Warn($"Failed to connect to {currentDevice.Name}. PhoneLineTransportDevice is not connected.");
                        return;
                    }
                }
                else
                {
                    logger.Error($"Failed to register {currentDevice.Name}. PhoneLineTransportDevice Access Denied: {accessStatus}");
                }
            }
            catch (Exception ex)
            {
                logger.Error($"Error registering phone device: {ex.Message}", ex);
            }
        });
    }

    private PhoneLine? _selectedPhoneLine;

    public async Task ConnectAsync()
    {
        if (SelectedDevice == null)
        {
            logger.Error("No device selected");
            return;
        }
        BluetoothDevice bluetoothDevice = await BluetoothDevice.FromIdAsync(SelectedDevice.Id);
        await Task.Delay(3000);
        PhoneLineTransportDevice? pltDevice = await PhoneLineTransportHelper.GetPhoneLineTransportFromBluetoothDevice(bluetoothDevice);

        if (pltDevice == null)
        {
            logger.Info("pltDevice was null");
            return;
        }
        DeviceAccessStatus accessStatus = await pltDevice.RequestAccessAsync();
        if (accessStatus == DeviceAccessStatus.Allowed)
        {
            var Connected = await pltDevice.ConnectAsync();
            if (Connected == false)
            {
                logger.Error($"Failed to connect {SelectedDevice.Name}");
                return;
            }
            logger.Info("Connected successfully");
            List<PhoneLine> phoneLinesAvailable = [];

            var lineEnumerationCompletion = new TaskCompletionSource<bool>();
            PhoneCallStore store = await PhoneCallManager.RequestStoreAsync();

            PhoneLineWatcher watcher = store.RequestLineWatcher();
            watcher.LineAdded += async (o, args) =>
            {
                phoneLinesAvailable.Add(await PhoneLine.FromIdAsync(args.LineId));
            };
            watcher.Stopped += (o, args) => lineEnumerationCompletion.TrySetResult(false);
            watcher.EnumerationCompleted += (o, args) => lineEnumerationCompletion.TrySetResult(true);
            watcher.Start();

            if (await lineEnumerationCompletion.Task)
            {
                logger.Info("PhoneLineWatcher enumeration completed.");
                _selectedPhoneLine = phoneLinesAvailable
                    .Where(pl => pl.TransportDeviceId == pltDevice.DeviceId)
                    .FirstOrDefault();
            }
            watcher.Stop();

            CallAsync();
        }
        else
        {
            logger.Error($"Unable to connect {SelectedDevice.Name}. PhoneLineTransportDevice Access Denied: {accessStatus}.");
        }
    }

    public async void CallAsync(string phoneNumber = "9961928758")
    {
        if (_selectedPhoneLine == null || _selectedPhoneLine?.CanDial == false)
        {
            logger.Error("no phone line");
            return;
        }
        var result = await _selectedPhoneLine!.DialWithResultAsync(phoneNumber, phoneNumber);
        var phoneCall = result.DialedCall;
        phoneCall.AudioDeviceChanged += AudioDeviceChanged;
        logger.Debug("Changing audio route");
        await phoneCall.ChangeAudioDeviceAsync(PhoneCallAudioDevice.LocalDevice);
        await Task.Delay(10000);
        await phoneCall.EndAsync();
    }

    private void AudioDeviceChanged(PhoneCall sender, object args)
    {
        logger.Debug("Audio device changed: {0}", sender.AudioDevice);
    }

    public async Task ConnectUsingRfcomm()
    {
        if (SelectedDevice == null) return;
       Guid HandsFreeProfileUuid = new("0000111f-0000-1000-8000-00805f9b34fb");
        try {
            BluetoothDevice device = await BluetoothDevice.FromIdAsync(SelectedDevice.Id);

            // HFP (Hands-Free Profile) service UUID
            // Check SDP first
            var result = await device.GetRfcommServicesAsync();
            if (result.Error != BluetoothError.Success)
            {
                // Can not get any services from SDP
                return;
            }

            var hfp = result.Services.Where(svc => svc.ServiceId.Uuid == HandsFreeProfileUuid).ToList();
            if (hfp.Count == 0)
            {
                return;
            }

            // Connect to the first service
            var service = hfp[0];
            var socket = new StreamSocket();
            
            await socket.ConnectAsync(
                service.ConnectionHostName,
                service.ConnectionServiceName,
                SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication);
                
            // Now we have a direct communication channel
            var writer = new DataWriter(socket.OutputStream);
            var reader = new DataReader(socket.InputStream);
            
            // Send AT commands directly
            logger.Info("Connected to HFP service natively");
            
            // Example: Query supported features
            writer.WriteString("AT+BRSF=191\r");
            await writer.StoreAsync();
            
            // Read response
            reader.InputStreamOptions = InputStreamOptions.Partial;
            await reader.LoadAsync(128);
            string response = reader.ReadString(reader.UnconsumedBufferLength);
            
            logger.Info("Device response: {0}", response);
        }
        catch (Exception ex) {
            logger.Error("Native connection error: {0}", ex.Message);
        }
    }

    public async Task EstablishPhoneConnection()
    {
        // First establish RFCOMM connection
        await ConnectUsingRfcomm();

        if (SelectedDevice == null) return;

        try
        {
            BluetoothDevice device = await BluetoothDevice.FromIdAsync(SelectedDevice.Id);

            // Now that we've established the RFCOMM connection first, try PhoneLineTransport again
            PhoneCallStore store = await PhoneCallManager.RequestStoreAsync();
            if (store == null)
            {
                logger.Error("Could not get PhoneCallStore");
                return;
            }


            // Register without user parameter first
            // Try direct access to lines
            var lines = await store.GetDefaultLineAsync();

            logger.Error("Lines: {0}", lines.ToString());
        }
        catch (Exception ex)
        {
            logger.Error("Phone connection error: {0}", ex);
        }
    }
}
