using System.Collections.Specialized;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Extensions;
using Sefirah.Services.Socket;

namespace Sefirah.Data.Models;

public partial class PairedDevice : ObservableObject
{
    public string Id { get; private set; }
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<string>? IpAddresses { get; set; } = [];
    public List<PhoneNumber>? PhoneNumbers { get; set; } = [];
    public ImageSource? Wallpaper { get; set; }

    public string ConnectionButtonText => ConnectionStatus ? "Connected/Text".GetLocalizedResource() : "Disconnected/Text".GetLocalizedResource();

    private bool connectionStatus;
    public bool ConnectionStatus 
    {
        get => connectionStatus;
        set
        {
            if (SetProperty(ref connectionStatus, value))
            {
                OnPropertyChanged(nameof(ConnectionButtonText));
            }
        }
    }

    private DeviceStatus? _status;
    public DeviceStatus? Status
    {
        get => _status;
        set
        {
            SetProperty(ref _status, value);
        }
    }

    private ServerSession? _session;
    public ServerSession? Session
    {
        get => _session;
        set
        {
            if (SetProperty(ref _session, value))
            {
                OnPropertyChanged(nameof(ConnectionButtonText));
            }
        }
    }

    private readonly IAdbService adbService;
    private readonly IUserSettingsService userSettingsService;

    private IDeviceSettingsService deviceSettings;
    public IDeviceSettingsService DeviceSettings
    {
        get => deviceSettings;
        private set => SetProperty(ref deviceSettings, value);
    }


    public PairedDevice(string Id)
    {
        this.Id = Id;
        userSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
        adbService = Ioc.Default.GetRequiredService<IAdbService>();
        adbService.AdbDevices.CollectionChanged += OnAdbDevicesChanged;
        deviceSettings = userSettingsService.GetDeviceSettings(Id);
    }

    public ObservableCollection<AdbDevice> ConnectedAdbDevices { get; set; } = [];

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
            // Use UI thread if available
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
