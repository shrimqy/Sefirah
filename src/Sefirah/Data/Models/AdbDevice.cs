using AdvancedSharpAdbClient.Models;
using Sefirah.Data.Enums;

namespace Sefirah.Data.Models;

public partial class AdbDevice : ObservableObject
{
    private string serial = string.Empty;
    public string Serial
    {
        get => serial;
        set
        {
            if (SetProperty(ref serial, value))
            {
                UpdateDisplayName();
            }
        }
    }

    private string model = string.Empty;
    public string Model
    {
        get => model;
        set
        {
            if (SetProperty(ref model, value))
            {
                UpdateDisplayName();
            }
        }
    }

    private string androidId = string.Empty;
    public string AndroidId
    {
        get => androidId;
        set => SetProperty(ref androidId, value);
    }

    private string displayName = string.Empty;
    public string DisplayName
    {
        get => displayName;
        set => SetProperty(ref displayName, value);
    }

    private DeviceState state = DeviceState.Unknown;
    public DeviceState State
    {
        get => state;
        set
        {
            if (SetProperty(ref state, value))
            {
                OnPropertyChanged(nameof(IsOnline));
                IsConnected = value is DeviceState.Online;
            }
        }
    }

    private DeviceType type;
    public DeviceType Type
    {
        get => type;
        set
        {
            if (SetProperty(ref type, value))
            {
                OnPropertyChanged(nameof(TypeDisplay));
            }
        }
    }

    private DeviceData? deviceData = null;
    public DeviceData? DeviceData
    {
        get => deviceData;
        set => SetProperty(ref deviceData, value);
    }

    private bool isConnected;
    public bool IsConnected
    {
        get => isConnected;
        set => SetProperty(ref isConnected, value);
    }

    public string TypeDisplay => Type is DeviceType.WIFI ? "WiFi" : "USB";
    
    public bool IsOnline => State is DeviceState.Online;

    private void UpdateDisplayName()
    {
        DisplayName = !string.IsNullOrEmpty(Model) ? $"{Model} ({Serial})" : Serial;
    }
}
