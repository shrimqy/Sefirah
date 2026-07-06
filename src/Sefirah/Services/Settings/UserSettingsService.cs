using Sefirah.Utils;
using Sefirah.Utils.Serialization;
using Sefirah.Utils.Serialization.Implementation;

namespace Sefirah.Services.Settings;
internal sealed class UserSettingsService : BaseJsonSettings, IUserSettingsService
{
    private IGeneralSettingsService _generalSettingsService;
    public IGeneralSettingsService GeneralSettingsService 
    { 
        get => GetSettingsService(ref _generalSettingsService);
    }

    private readonly Dictionary<string, IDeviceSettingsService> _deviceSettingsCache = [];

    public UserSettingsService()
    {
        SettingsSerializer = new SettingsSerializer();
        Initialize(LocalAppPaths.GetUserSettingsPath());

        JsonSettingsSerializer = new JsonSettingsSerializer();
        JsonSettingsDatabase = new CachingJsonSettingsDatabase(SettingsSerializer, JsonSettingsSerializer);
    }

    public IDeviceSettingsService GetDeviceSettings(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or whitespace", nameof(deviceId));

        // Return cached instance if available
        if (_deviceSettingsCache.TryGetValue(deviceId, out var cachedSettings))
        {
            return cachedSettings;
        }

        // Create new device-specific settings instance (it manages its own file)
        var deviceSettings = new DeviceSettingsService(deviceId);
        _deviceSettingsCache[deviceId] = deviceSettings;

        return deviceSettings;
    }

    private static TSettingsService GetSettingsService<TSettingsService>(ref TSettingsService settingsServiceMember)
        where TSettingsService : class, IBaseSettingsService
    {
        settingsServiceMember ??= Ioc.Default.GetService<TSettingsService>()!;

        return settingsServiceMember;
    }
}
