using Sefirah.Actions;
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

    public StorageMountPreference StorageMountPreference
    {
        get => Get(StorageMountPreference.Auto);
        set => Set(value);
    }

    public List<ActionItem> Actions
    {
        get => Get<List<ActionItem>>([]);
        private set => Set(value);
    }

    public void SetActions(IEnumerable<ActionItem> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        Actions = actions.Select(static a => a.Clone()).ToList();
    }

    public void AddAction(ActionItem action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var actions = Actions.ToList();
        actions.Add(action.Clone());
        Actions = actions;
    }

    public void UpdateAction(ActionItem action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var actions = Actions.ToList();
        var index = actions.FindIndex(a => a.Id == action.Id);
        if (index != -1)
        {
            actions[index] = action.Clone();
            Actions = actions;
        }
    }

    public void RemoveAction(ActionItem action)
    {
        ArgumentNullException.ThrowIfNull(action);
        var actions = Actions.ToList();
        var index = actions.FindIndex(a => a.Id == action.Id);
        if (index != -1)
        {
            actions.RemoveAt(index);
            Actions = actions;
        }
    }
}
