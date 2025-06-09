using Sefirah.Data.EventArguments;

namespace Sefirah.Data.Contracts;
public interface IUserSettingsService : IBaseSettingsService
{
    event EventHandler<SettingChangedEventArgs> OnSettingChangedEvent;

    IGeneralSettingsService GeneralSettingsService { get; }

    /// <summary>
    /// Gets the global feature settings service (for backwards compatibility and global settings)
    /// </summary>
    IFeatureSettingsService FeatureSettingsService { get; }

    /// <summary>
    /// Gets the device-specific settings for a given device ID
    /// </summary>
    /// <param name="deviceId">The unique identifier for the device</param>
    /// <returns>Device-specific settings service</returns>
    IDeviceSettingsService GetDeviceSettings(string deviceId);
}
