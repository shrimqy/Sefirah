using Sefirah.Data.Contracts;
using Sefirah.Platforms.Desktop.Tray;
using Sefirah.Platforms.Desktop.Tray.DBus;
using Tmds.DBus.Protocol;

namespace Sefirah.Platforms.Desktop.Services;

/// <summary>
/// Linux system tray using the StatusNotifierItem D-Bus protocol.
/// </summary>
public sealed class DesktopSystemTrayService : ISystemTrayService
{
    private const string WatcherService = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";
    private const string MenuPath = "/com/castle/sefirah/dbusmenu/0";

    private readonly ILogger<DesktopSystemTrayService> logger;
    private DBusConnection? connection;
    private LinuxStatusNotifierItemHandler? itemHandler;
    private LinuxTrayMenuHandler? menuHandler;
    private string? serviceName;
    private bool disposed;

    public DesktopSystemTrayService(ILogger<DesktopSystemTrayService> logger)
    {
        this.logger = logger;

        if (OperatingSystem.IsLinux())
            _ = InitializeAsync();
    }

    public bool IsAvailable { get; private set; }

    private async Task InitializeAsync()
    {
        try
        {
            var sessionAddress = DBusAddress.Session
                ?? throw new InvalidOperationException("D-Bus session bus is not available.");

            connection = new DBusConnection(sessionAddress);
            await connection.ConnectAsync();

            serviceName = $"org.kde.StatusNotifierItem-{Environment.ProcessId}-1";

            menuHandler = new LinuxTrayMenuHandler(connection, logger, MenuPath);
            itemHandler = new LinuxStatusNotifierItemHandler(connection, logger, menuHandler, LinuxTrayIconLoader.GetTrayIconPath());

            connection.AddMethodHandler(menuHandler);
            connection.AddMethodHandler(itemHandler);

            itemHandler.Activated += (_, _) => App.TrayToggleWindow();

            if (!await connection.TryRequestNameAsync(serviceName, RequestNameOptions.ReplaceExisting))
            {
                logger.Warn($"Failed to acquire D-Bus tray service name: {serviceName}");
                return;
            }

            if (await IsWatcherAvailableAsync(sessionAddress))
                await RegisterTrayAsync();
            else
                logger.Warn("StatusNotifierWatcher is not available; tray icon will not appear until a tray host starts");
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to initialize Linux system tray", ex);
            Dispose();
        }
    }

    private async Task RegisterTrayAsync()
    {
        if (connection is null || itemHandler is null || serviceName is null)
            return;

        try
        {
            var iconPath = itemHandler.IconPath;
            if (File.Exists(iconPath))
            {
                var pixmaps = LinuxTrayIconLoader.LoadPixmaps(iconPath);
                itemHandler.SetIcon(pixmaps);
                logger.Info($"Loaded tray icon from {iconPath} ({pixmaps.Length} sizes, {pixmaps[0].Pixels.Length} bytes)");
            }
            else
            {
                logger.Warn($"Tray icon file missing at {iconPath}; using fallback pixel");
            }

            var watcher = new StatusNotifierWatcher(connection, WatcherService, WatcherPath);
            await watcher.RegisterStatusNotifierItemAsync(serviceName);

            IsAvailable = true;
            logger.Info($"Linux tray icon registered at {serviceName} (menu at {MenuPath})");
        }
        catch (Exception ex)
        {
            logger.Warn("Failed to register Linux tray icon", ex);
            IsAvailable = false;
        }
    }

    private static async Task<bool> IsWatcherAvailableAsync(string sessionAddress)
    {
        try
        {
            using var probe = new DBusConnection(sessionAddress);
            await probe.ConnectAsync();
            return await GetNameOwnerAsync(probe, WatcherService) is not null;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string?> GetNameOwnerAsync(DBusConnection dbus, string name)
    {
        try
        {
            MessageBuffer message;
            using (var writer = dbus.GetMessageWriter())
            {
                writer.WriteMethodCallHeader(
                    destination: "org.freedesktop.DBus",
                    path: "/org/freedesktop/DBus",
                    @interface: "org.freedesktop.DBus",
                    signature: "s",
                    member: "GetNameOwner");
                writer.WriteString(name);
                message = writer.CreateMessage();
            }

            return await dbus.CallMethodAsync(
                message,
                static (Message response, object? _) => response.GetBodyReader().ReadString());
        }
        catch (DBusErrorReplyException ex) when (ex.ErrorName is "org.freedesktop.DBus.Error.NameHasNoOwner")
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        IsAvailable = false;

        if (connection is not null)
        {
            if (itemHandler is not null)
                connection.RemoveMethodHandler(itemHandler.Path);
            if (menuHandler is not null)
                connection.RemoveMethodHandler(menuHandler.Path);
            connection.Dispose();
        }

        connection = null;
        itemHandler = null;
        menuHandler = null;
    }
}
