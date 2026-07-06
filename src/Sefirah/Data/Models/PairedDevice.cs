using System.Collections.Specialized;
using CommunityToolkit.WinUI;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

public partial class PairedDevice : BaseRemoteDevice
{
    private string address = string.Empty;
    public string Address
    {
        get => address;
        set
        {
            if (SetProperty(ref address, value))
                RefreshAddressConnectionStates();
        }
    }

    private ObservableCollection<AddressEntry> addresses = [];
    public ObservableCollection<AddressEntry> Addresses
    {
        get => addresses;
        set
        {
            if (SetProperty(ref addresses, value))
                RefreshAddressConnectionStates();
        }
    }

    /// <summary>
    /// Gets enabled addresses
    /// </summary>
    public List<string> GetEnabledAddresses()
    {
        var enabledAddresses = Addresses
            .Where(ip => ip.IsEnabled)
            .Select(ip => ip.Address);
        
        // If no addresses are enabled, return all addresses
        if (!enabledAddresses.Any())
        {
            return Addresses
                .Select(ip => ip.Address)
                .ToList();
        }
        
        return enabledAddresses.ToList();
    }

    /// <summary>
    /// Adds an address to the list if it is not already present.
    /// </summary>
    /// <returns>True if the address was added.</returns>
    public bool TryAddAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        address = address.Trim();
        if (Addresses.Any(a => a.Address.Equals(address, StringComparison.OrdinalIgnoreCase)))
            return false;

        Addresses.Add(new AddressEntry
        {
            Address = address,
            IsEnabled = true
        });
        return true;
    }

    public int Port { get; set; } = 5150;

    public List<PhoneNumber> PhoneNumbers { get; set; } = [];

    private ImageSource? wallpaper;
    public ImageSource? Wallpaper
    {
        get => wallpaper;
        set => SetProperty(ref wallpaper, value);
    }

    private ConnectionStatus connectionStatus = new Disconnected();
    public ConnectionStatus ConnectionStatus 
    {
        get => connectionStatus;
        set
        {
            if (SetProperty(ref connectionStatus, value))
            {
                OnPropertyChanged(nameof(ConnectionStatusText));
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(IsConnecting));
                OnPropertyChanged(nameof(IsForcedDisconnect));
                OnPropertyChanged(nameof(IsConnectedOrConnecting));
                RefreshAddressConnectionStates();
            }
        }
    }

    private void RefreshAddressConnectionStates()
    {
        foreach (var entry in Addresses)
        {
            entry.IsConnected = IsConnected
                && !string.IsNullOrEmpty(Address)
                && entry.Address.Equals(Address, StringComparison.OrdinalIgnoreCase);
        }
    }

    public string ConnectionStatusText
    {
        get
        {
            return ConnectionStatus switch
            {
                Connected => "Connected.Text".GetLocalizedResource(),
                Connecting => "Connecting",
                Disconnected => "Disconnected.Text".GetLocalizedResource(),
                _ => "Unknown"
            };
        }
    }
    public bool IsDisconnected => ConnectionStatus.IsDisconnected;
    public bool IsConnected => ConnectionStatus.IsConnected;
    public bool IsForcedDisconnect => ConnectionStatus.IsForcedDisconnect;
    public bool IsConnecting => ConnectionStatus.IsConnecting;
    public bool IsConnectedOrConnecting => ConnectionStatus.IsConnectedOrConnecting;

    private BatteryState? batteryStatus;
    public BatteryState? BatteryStatus
    {
        get => batteryStatus;
        set => SetProperty(ref batteryStatus, value);
    }

    private int ringerMode = -1;
    public int RingerMode
    {
        get => ringerMode;
        set => SetProperty(ref ringerMode, value);
    }

    private bool dndEnabled;
    public bool DndEnabled
    {
        get => dndEnabled;
        set => SetProperty(ref dndEnabled, value);
    }

    public IReadOnlyList<AudioStream> Streams { get; } =
    [
        new(AudioStreamType.Media),
        new(AudioStreamType.Ring),
        new(AudioStreamType.Notification),
        new(AudioStreamType.Alarm),
        new(AudioStreamType.VoiceCall)
    ];

    public void UpdateStreamLevel(AudioStreamType streamType, int level)
    {
        var stream = Streams.FirstOrDefault(s => s.StreamType == streamType);
        stream?.Level = level;
    }

    public ObservableCollection<AdbDevice> ConnectedAdbDevices { get; set; } = [];

    public ObservableCollection<MediaSession> RemotePlaybackSessions { get; } = [];

    private bool isActiveDevice;
    public bool IsActiveDevice
    {
        get => isActiveDevice;
        set => SetProperty(ref isActiveDevice, value);
    }

    private string? callsTransportDeviceId;
    public string? CallsTransportDeviceId
    {
        get => callsTransportDeviceId;
        set => SetProperty(ref callsTransportDeviceId, value);
    }

    private string? bluetoothAddress;
    public string? BluetoothAddress
    {
        get => bluetoothAddress;
        set => SetProperty(ref bluetoothAddress, value);
    }

    private string? bluetoothClassicDeviceId;
    public string? BluetoothClassicDeviceId
    {
        get => bluetoothClassicDeviceId;
        set => SetProperty(ref bluetoothClassicDeviceId, value);
    }

    private readonly IAdbService adbService = Ioc.Default.GetRequiredService<IAdbService>();
    private readonly IUserSettingsService userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();

    private IDeviceSettingsService deviceSettings;
    public IDeviceSettingsService DeviceSettings
    {
        get => deviceSettings;
        private set => SetProperty(ref deviceSettings, value);
    }

    public PairedDevice(string deviceId)
    {
        Id = deviceId;
        adbService.AdbDevices.CollectionChanged += OnAdbDevicesChanged;
        deviceSettings = userSettingsService.GetDeviceSettings(deviceId);
    }

    public bool HasAdbConnection
    {
        get
        {
            try
            {
                return adbService?.AdbDevices.Any(adbDevice => 
                    adbDevice.IsOnline && 
                    (
                        (!string.IsNullOrEmpty(adbDevice.AndroidId) && adbDevice.AndroidId == Id) ||
                        (string.IsNullOrEmpty(adbDevice.AndroidId) && 
                         !string.IsNullOrEmpty(adbDevice.Model) && 
                         !string.IsNullOrEmpty(Model) &&
                         (Model.Equals(adbDevice.Model, StringComparison.OrdinalIgnoreCase) ||
                          Model.Contains(adbDevice.Model, StringComparison.OrdinalIgnoreCase) ||
                          adbDevice.Model.Contains(Model, StringComparison.OrdinalIgnoreCase)))
                    )) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }

    private void OnAdbDevicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshConnectedAdbDevices();
        OnPropertyChanged(nameof(HasAdbConnection));
    }

    private async void RefreshConnectedAdbDevices()
    {
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                ConnectedAdbDevices.Clear();

                var devices = adbService.AdbDevices
                    .Where(adbDevice => adbDevice.IsOnline && 
                        (
                            (!string.IsNullOrEmpty(adbDevice.AndroidId) && adbDevice.AndroidId == Id) ||
                            (string.IsNullOrEmpty(adbDevice.AndroidId) && 
                                !string.IsNullOrEmpty(adbDevice.Model) && 
                                !string.IsNullOrEmpty(Model) &&
                                (Model.Equals(adbDevice.Model, StringComparison.OrdinalIgnoreCase) ||
                                Model.Contains(adbDevice.Model, StringComparison.OrdinalIgnoreCase) ||
                                adbDevice.Model.Contains(Model, StringComparison.OrdinalIgnoreCase)))
                        ))
                    .ToList();

                ConnectedAdbDevices.AddRange(devices);
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in RefreshConnectedAdbDevices: {ex.Message}");
        }
    }
}

