using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Contracts;
using Sefirah.Extensions;
using Sefirah.Helpers;
using Sefirah.Services.Socket;
using Sefirah.Services;

namespace Sefirah.Data.Models;

public partial class PairedDevice : ObservableObject
{
    public string Id { get; set; }
    public string Name { get; set; }
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

    // ADB Connection Properties
    public bool HasAdbConnection
    {
        get
        {
            try
            {
                var adbService = Ioc.Default.GetService<IAdbService>();
                if (adbService == null) return false;

                return adbService.AdbDevices.Any(adbDevice => 
                    adbDevice.IsOnline && 
                    !string.IsNullOrEmpty(adbDevice.AndroidId) && 
                    adbDevice.AndroidId == Id);
            }
            catch
            {
                return false;
            }
        }
    }

    public ObservableCollection<AdbDevice> ConnectedAdbDevices
    {
        get
        {
            try
            {
                var adbService = Ioc.Default.GetService<IAdbService>();
                if (adbService == null) return new ObservableCollection<AdbDevice>();

                var connectedDevices = adbService.AdbDevices
                    .Where(adbDevice => adbDevice.IsOnline && 
                                       !string.IsNullOrEmpty(adbDevice.AndroidId) && 
                                       adbDevice.AndroidId == Id)
                    .ToList();

                return new ObservableCollection<AdbDevice>(connectedDevices);
            }
            catch
            {
                return new ObservableCollection<AdbDevice>();
            }
        }
    }

    private IDeviceSettingsService? _deviceSettings;
    public IDeviceSettingsService DeviceSettings
    {
        get
        {
            if (_deviceSettings == null)
            {
                var userSettingsService = Ioc.Default.GetService<IUserSettingsService>();
                _deviceSettings = userSettingsService?.GetDeviceSettings(Id);
            }
            return _deviceSettings!;
        }
    }

    public static async Task<PairedDevice> FromDeviceInfo(DeviceInfo device, string IpAddress)
    {
        var wallPaperBytes = string.IsNullOrEmpty(device.Avatar) ? null : Convert.FromBase64String(device.Avatar);
        var wallPaper = await ImageHelper.ToBitmapAsync(wallPaperBytes);
        return new PairedDevice
        {
            Id = device.DeviceId,
            Name = device.DeviceName,
            IpAddresses = [IpAddress],
            PhoneNumbers = device.PhoneNumbers,
            Wallpaper = wallPaper
        };
    }

    public static async Task<PairedDevice> FromRemoteDevice(RemoteDeviceEntity device)
    {
        var wallPaperBytes = device.WallpaperBytes;
        BitmapImage? wallPaper = null;
        
        if (wallPaperBytes != null)
        {
            var dispatcher = App.MainWindow?.DispatcherQueue;
            if (dispatcher != null)
            {
                await dispatcher.EnqueueAsync(async () =>
                {
                    wallPaper = await ImageHelper.ToBitmapAsync(wallPaperBytes);
                });
            }
        }

        return new PairedDevice
        {
            Id = device.DeviceId,
            Name = device.Name,
            IpAddresses = device.IpAddresses,
            PhoneNumbers = device.PhoneNumbers,
            Wallpaper = wallPaper,
        };
    }

    /// <summary>
    /// Call this method to refresh ADB connection status when ADB devices change
    /// </summary>
    public void RefreshAdbStatus()
    {
        OnPropertyChanged(nameof(HasAdbConnection));
        OnPropertyChanged(nameof(ConnectedAdbDevices));
    }
}
