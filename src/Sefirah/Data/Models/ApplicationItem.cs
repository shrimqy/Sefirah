namespace Sefirah.Data.Models;

public partial class ApplicationItem(string deviceId, string packageName, string appName, string? iconPath) : ObservableObject
{
    private string deviceId = deviceId;
    public string DeviceId
    {
        get => deviceId;
        set => SetProperty(ref deviceId, value);
    }

    private string packageName = packageName;
    public string PackageName
    {
        get => packageName;
        set => SetProperty(ref packageName, value);
    }

    private string appName = appName;
    public string AppName
    {
        get => appName;
        set => SetProperty(ref appName, value);
    }

    private string? iconPath = iconPath;
    public string? IconPath
    {
        get => iconPath;
        set => SetProperty(ref iconPath, value);
    }

    private bool pinned;
    public bool Pinned
    {
        get => pinned;
        set => SetProperty(ref pinned, value);
    }

    private NotificationFilter filter = NotificationFilter.ToastFeed;
    public NotificationFilter Filter
    {
        get => filter;
        set => SetProperty(ref filter, value);
    }

    private bool isLoading;
    public bool IsLoading
    {
        get => isLoading;
        set => SetProperty(ref isLoading, value);
    }

    private bool appShortcutRegistered;
    public bool AppShortcutRegistered
    {
        get => appShortcutRegistered;
        set => SetProperty(ref appShortcutRegistered, value);
    }

    private string? selectedNotificationFilter;
    public string SelectedNotificationFilter
    {
        get => selectedNotificationFilter ?? NotificationFilterTypes[Filter];
        set => SetProperty(ref selectedNotificationFilter, value);
    }

    public static Dictionary<NotificationFilter, string> NotificationFilterTypes { get; } = new()
    {
        { NotificationFilter.ToastFeed, "NotificationFilterToastFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Feed, "NotificationFilterFeed.Content".GetLocalizedResource() },
        { NotificationFilter.Disabled, "NotificationFilterDisabled.Content".GetLocalizedResource() }
    };
}
