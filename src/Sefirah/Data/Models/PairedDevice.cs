using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Contracts;
using Sefirah.Extensions;
using Sefirah.Helpers;
using Sefirah.Services.Socket;

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
}
