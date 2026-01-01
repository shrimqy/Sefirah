using System.Collections.Specialized;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models.Messages;

namespace Sefirah.Data.Models;

public partial class PairedDevice : BaseRemoteDevice
{
    public List<IpAddressEntry> IpAddresses { get; set; } = [];

    /// <summary>
    /// Gets enabled IP addresses sorted by priority (0 = highest priority)
    /// </summary>
    public List<string> GetEnabledIpAddresses()
    {
        return IpAddresses
            .Where(ip => ip.IsEnabled)
            .OrderBy(ip => ip.Priority)
            .Select(ip => ip.IpAddress)
            .ToList();
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
    public bool IsConnectedOrConnecting => ConnectionStatus.IsConnectedOrConnecting;

    private DeviceStatus? status;
    public DeviceStatus? Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

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
