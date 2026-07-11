using Microsoft.Win32;

namespace Sefirah.Platforms.Windows.Helpers;

internal static class SystemThemeHelper
{
    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>
    /// Whether Windows is using the light system theme (Settings → Personalization → Windows color mode).
    /// </summary>
    public static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
        return key?.GetValue("SystemUsesLightTheme") is int value && value == 1;
    }
}
