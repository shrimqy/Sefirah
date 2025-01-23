using Sefirah.App.Data.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;

namespace Sefirah.App.Data.Contracts;
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
    void ApplyTheme(Window? window = null, AppWindowTitleBar? titleBar = null, Theme? theme = null);

}
