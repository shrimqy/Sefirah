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

/// <summary>
/// Preferred backend for mounting device storage over SFTP on Linux.
/// </summary>
public enum StorageMountPreference
{
    /// <summary>
    /// Prefer sshfs on KDE/Plasma, GVfs elsewhere; fall back if the preferred backend fails.
    /// </summary>
    Auto,

    /// <summary>
    /// Use GNOME GVfs (<c>gio mount</c>), with sshfs as fallback.
    /// </summary>
    Gvfs,

    /// <summary>
    /// Use sshfs (FUSE), with GVfs as fallback.
    /// </summary>
    Sshfs
}
