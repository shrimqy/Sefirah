namespace Sefirah.Data.Enums;

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

public enum AudioOutputModeType
{
    Desktop,
    Remote,
    Both
}

public enum ScrcpyDevicePreferenceType
{
    Auto,
    Usb,
    Tcpip,
    AskEverytime
}
