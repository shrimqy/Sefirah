using Sefirah.Data.EventArguments;
using Sefirah.Utils.Serialization.Implementation;
using System.Runtime.CompilerServices;

namespace Sefirah.Utils.Serialization;

/// <summary>
/// A base class for device-specific settings that stores settings in separate files per device.
/// </summary>
internal abstract class BaseDeviceAwareJsonSettings : BaseObservableJsonSettings
{
    private readonly string _deviceId;

    protected BaseDeviceAwareJsonSettings(string deviceId, ISettingsSharingContext parentContext)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or whitespace", nameof(deviceId));

        _deviceId = deviceId;
        
        // Initialize our own serializers and database for this device
        SettingsSerializer = new SettingsSerializer();
        JsonSettingsSerializer = new JsonSettingsSerializer();
        
        // Create device-specific file path
        var deviceFileName = GetDeviceFileName(deviceId);
        var deviceFilePath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path, 
            Constants.LocalSettings.SettingsFolderName, 
            "Devices",
            deviceFileName);

        // Ensure the Devices directory exists
        var devicesDirectory = Path.GetDirectoryName(deviceFilePath);
        if (!Directory.Exists(devicesDirectory))
        {
            Directory.CreateDirectory(devicesDirectory);
        }

        // Initialize with device-specific file
        Initialize(deviceFilePath);
        
        JsonSettingsDatabase = new CachingJsonSettingsDatabase(SettingsSerializer, JsonSettingsSerializer);
    }

    public string DeviceId => _deviceId;

    /// <summary>
    /// Creates a safe filename for the device settings file
    /// </summary>
    private static string GetDeviceFileName(string deviceId)
    {
        // Replace invalid filename characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeDeviceId = string.Join("_", deviceId.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        
        return $"device_{safeDeviceId}.json";
    }

    /// <summary>
    /// Sets a setting value (no prefix needed since we have our own file)
    /// </summary>
    protected override bool Set<TValue>(TValue? value, [CallerMemberName] string propertyName = "") where TValue : default
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        if (JsonSettingsDatabase is not null &&
            (!JsonSettingsDatabase.GetValue<TValue>(propertyName)?.Equals(value) ?? true) &&
            JsonSettingsDatabase.SetValue(propertyName, value))
        {
            RaiseOnSettingChangedEvent(this, new SettingChangedEventArgs(propertyName, value));
            OnPropertyChanged(propertyName);
            return true;
        }

        return false;
    }
} 