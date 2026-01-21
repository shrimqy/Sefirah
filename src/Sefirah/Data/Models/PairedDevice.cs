using System.Collections.Specialized;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

public partial class PairedDevice : BaseRemoteDevice
{
    public List<AddressEntry> Addresses { get; set; } = [];

    /// <summary>
    /// Gets enabled addresses sorted by priority.
    /// </summary>
    public List<string> GetEnabledAddresses()
    {
        var enabledAddresses = Addresses
            .Where(ip => ip.IsEnabled)
            .OrderBy(ip => ip.Priority)
            .Select(ip => ip.Address);
        
        // If no addresses are enabled, return all addresses
        if (!enabledAddresses.Any())
        {
            return Addresses
                .OrderBy(ip => ip.Priority)
                .Select(ip => ip.Address)
                .ToList();
        }
        
        return enabledAddresses.ToList();
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
                OnPropertyChanged(nameof(IsForcedDisconnect));
                OnPropertyChanged(nameof(IsConnectedOrConnecting));
            }
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

    private BatteryStatus? batteryStatus;
    public BatteryStatus? BatteryStatus
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

    public Audio Audio { get; } = new();

    public ObservableCollection<AdbDevice> ConnectedAdbDevices { get; set; } = [];

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

