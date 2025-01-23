using Sefirah.App.Data.EventArguments;

namespace Sefirah.App.Data.Contracts;
public interface IUserSettingsService : IBaseSettingsService
{
    event EventHandler<SettingChangedEventArgs> OnSettingChangedEvent;

    IGeneralSettingsService GeneralSettingsService { get; }

    IFeatureSettingsService FeatureSettingsService { get; }
}
