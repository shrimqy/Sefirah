using Microsoft.UI.Windowing;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
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

    /// <summary>
    /// Event that fires when theme changes
    /// </summary>
    event EventHandler? ThemeChanged;

    /// <summary>
    /// Applies the theme to a specific window or the main window
    /// </summary>
    /// <param name="window">Optional window to apply theme to</param>
    /// <param name="titleBar">Optional titlebar to apply theme to</param>
    /// <param name="theme">Optional specific theme to apply</param>
    void ApplyTheme(Window? window = null, AppWindowTitleBar? titleBar = null, Theme? theme = null, bool callThemeModeChangedEvent = true);

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
