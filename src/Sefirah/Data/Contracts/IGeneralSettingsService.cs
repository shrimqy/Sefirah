using Sefirah.Actions;

namespace Sefirah.Data.Contracts;

public interface IGeneralSettingsService : IBaseSettingsService, INotifyPropertyChanged
{
    /// <summary>
    /// Gets or sets the startup option for the application.
    /// </summary>
    StartupOptions StartupOption { get; set; }

    /// <summary>
    /// Gets or sets the theme for the application.
    /// </summary>
    Theme Theme { get; set; }

    BackdropMaterialType BackdropMaterial { get; set; }

    /// <summary>
    /// Gets or sets the path for scrcpy.
    /// </summary>
    string ScrcpyPath { get; set; }

    /// <summary>
    /// Gets or sets the path for adb.
    /// </summary>
    string AdbPath { get; set; }

    /// <summary>
    /// Gets or sets the path for remote storage.
    /// </summary>
    string RemoteStoragePath { get; set; }

    /// <summary>
    /// Gets or sets the path for received files.
    /// </summary>
    string ReceivedFilesPath { get; set; }

    /// <summary>
    /// Gets or sets how device storage is mounted over SFTP on Linux (GVfs vs sshfs).
    /// </summary>
    StorageMountPreference StorageMountPreference { get; set; }

    /// <summary>
    /// Gets the list of custom actions.
    /// </summary>
    List<ActionItem> Actions { get; }

    /// <summary>
    /// Replaces the full actions list (e.g. after reordering).
    /// </summary>
    void SetActions(IEnumerable<ActionItem> actions);

    /// <summary>
    /// Adds a new action to the settings.
    /// </summary>
    void AddAction(ActionItem action);

    /// <summary>
    /// Updates an existing action in the settings.
    /// </summary>
    void UpdateAction(ActionItem action);

    /// <summary>
    /// Removes an action from the settings.
    /// </summary>
    void RemoveAction(ActionItem action);
}
