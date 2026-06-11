using Sefirah.Data.Models.Actions;
using Sefirah.Utils.Serialization;

namespace Sefirah.Services.Settings;

internal sealed partial class GeneralSettingsService : BaseObservableJsonSettings, IGeneralSettingsService
{
    public GeneralSettingsService(ISettingsSharingContext settingsSharingContext)
    {
        RegisterSettingsContext(settingsSharingContext);
    }

    public BackdropMaterialType BackdropMaterial
    {
        get => Get(BackdropMaterialType.Mica);
        set => Set(value);
    }

    public StartupOptions StartupOption
    {
        get => Get(StartupOptions.InTray);
        set => Set(value);
    }

    public Theme Theme
    {
        get => Get(Theme.Default);
        set => Set(value);
    }

    public string RemoteStoragePath
    {
        get => Get(Constants.UserEnvironmentPaths.DefaultRemoteDevicePath);
        set => Set(value);
    }

    public string ReceivedFilesPath
    {
        get => Get(Constants.UserEnvironmentPaths.DownloadsPath);
        set => Set(value);
    }

    public string ScrcpyPath
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public string AdbPath
    {
        get => Get(string.Empty);
        set => Set(value);
    }

    public List<BaseAction> Actions
    {
        get => Get<List<BaseAction>>([]);
        set => Set(value);
    }

    public void AddAction(BaseAction action)
    {
        var actions = Actions.ToList();
        actions.Add(action);
        Actions = actions;
    }

    public void UpdateAction(BaseAction action)
    {
        var actions = Actions.ToList();
        var index = actions.FindIndex(a => a.Id == action.Id);
        if (index != -1)
        {
            actions.RemoveAt(index);
            actions.Insert(index, action);
            Actions = actions;
        }
    }

    public void RemoveAction(BaseAction action)
    {
        var actions = Actions.ToList();
        var index = actions.FindIndex(a => a.Id == action.Id);
        if (index != -1)
        {
            actions.RemoveAt(index);
            Actions = actions;
        }
    }
}
