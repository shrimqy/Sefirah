﻿using Microsoft.UI.Xaml.Media;
using Sefirah.App.Data.Models;

namespace Sefirah.App.Data.AppDatabase.Models;

public class RemoteDeviceEntity : BaseEntity
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string>? IpAddresses { get; set; }
    public byte[]? SharedSecret { get; set; }
    public byte[]? WallpaperBytes { get; set; }

    public List<PhoneNumber>? PhoneNumbers { get; set; } = [];

    private ImageSource? _wallpaperImage;
    public ImageSource? WallpaperImage
    {
        get => _wallpaperImage;
        set => Set(ref _wallpaperImage, value);
    }

    private DateTime? _lastConnected;
    public DateTime? LastConnected
    {
        get => _lastConnected;
        set => Set(ref _lastConnected, value);
    }
}
