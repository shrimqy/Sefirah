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

    protected BaseDeviceAwareJsonSettings(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or whitespace", nameof(deviceId));

        _deviceId = deviceId;

        SettingsSerializer = new SettingsSerializer();
        JsonSettingsSerializer = new JsonSettingsSerializer();

        LocalAppPaths.EnsureDeviceFolder(deviceId);
        Initialize(LocalAppPaths.GetDeviceSettingsPath(deviceId));

        JsonSettingsDatabase = new CachingJsonSettingsDatabase(SettingsSerializer, JsonSettingsSerializer);
    }

    public string DeviceId => _deviceId;

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
