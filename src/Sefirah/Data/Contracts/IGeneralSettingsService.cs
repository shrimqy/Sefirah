using Sefirah.Data.Models.Actions;

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
    /// Gets or sets the list of custom actions.
    /// </summary>
    List<BaseAction> Actions { get; set; }

    /// <summary>
    /// Adds a new action to the settings.
    /// </summary>
    void AddAction(BaseAction action);

    /// <summary>
    /// Updates an existing action in the settings.
    /// </summary>
    void UpdateAction(BaseAction action);

    /// <summary>
    /// Removes an action from the settings.
    /// </summary>
    void RemoveAction(BaseAction action);
}
