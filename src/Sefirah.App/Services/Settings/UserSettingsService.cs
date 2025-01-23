using Sefirah.App.Data.Contracts;
using Sefirah.App.Utils.Serialization;
using Sefirah.App.Utils.Serialization.Implementation;
using Windows.Storage;

namespace Sefirah.App.Services.Settings;
internal sealed class UserSettingsService : BaseJsonSettings, IUserSettingsService
{
    private IGeneralSettingsService _generalSettingsService;
    public IGeneralSettingsService GeneralSettingsService 
    { 
        get => GetSettingsService(ref _generalSettingsService);
    }

    private IFeatureSettingsService _featureSettingsService;
    public IFeatureSettingsService FeatureSettingsService 
    { 
        get => GetSettingsService(ref _featureSettingsService); 
    }

    public UserSettingsService()
    {
        SettingsSerializer = new SettingsSerializer();

        Initialize(Path.Combine(ApplicationData.Current.LocalFolder.Path, Constants.LocalSettings.SettingsFolderName, Constants.LocalSettings.UserSettingsFileName));

        JsonSettingsSerializer = new JsonSettingsSerializer();
        JsonSettingsDatabase = new CachingJsonSettingsDatabase(SettingsSerializer, JsonSettingsSerializer);
    }

    private TSettingsService GetSettingsService<TSettingsService>(ref TSettingsService settingsServiceMember)
    where TSettingsService : class, IBaseSettingsService
    {
        settingsServiceMember ??= Ioc.Default.GetService<TSettingsService>()!;

        return settingsServiceMember;
    }
}
