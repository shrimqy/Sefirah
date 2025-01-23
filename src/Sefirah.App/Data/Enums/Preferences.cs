namespace Sefirah.App.Data.Enums;

public enum Theme
{
    Default,
    Light,
    Dark
}

public enum StartupOptions
{
    Disabled,
    Minimized,
    InTray,
    Maximized
}

public enum NotificationFilter
{
    Disabled,
    Feed,
    ToastFeed
}

public enum NotificationLaunchPreference
{
    Nothing,
    OpenInRemoteDevice,
    Dynamic
}