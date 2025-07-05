using AdvancedSharpAdbClient.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Sefirah.Data.Models;

public class AdbDevice : ObservableObject
{
    private string _serial = string.Empty;
    public string Serial
    {
        get => _serial;
        set
        {
            if (SetProperty(ref _serial, value))
            {
                UpdateDisplayName();
            }
        }
    }

    private string _model = string.Empty;
    public string Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value))
            {
                UpdateDisplayName();
            }
        }
    }

    private string _androidId = string.Empty;
    public string AndroidId
    {
        get => _androidId;
        set => SetProperty(ref _androidId, value);
    }

    private DeviceState _state = DeviceState.Unknown;
    public DeviceState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsOnline));
                IsConnected = value == DeviceState.Online;
            }
        }
    }

    private DeviceType _type;
    public DeviceType Type
    {
        get => _type;
        set
        {
            if (SetProperty(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeDisplay));
            }
        }
    }

    private DeviceData? _deviceData = null;
    public DeviceData? DeviceData
    {
        get => _deviceData;
        set => SetProperty(ref _deviceData, value);
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetProperty(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionStatus));
            }
        }
    }

    private string _displayName = string.Empty;
    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";
    
    public string TypeDisplay => Type == DeviceType.WIFI ? "WiFi" : "USB";
    
    public bool IsOnline => State == DeviceState.Online;

    private void UpdateDisplayName()
    {
        DisplayName = !string.IsNullOrEmpty(Model) ? $"{Model} ({Serial})" : Serial;
    }
}

public enum DeviceType
{
    USB,
    WIFI
} 