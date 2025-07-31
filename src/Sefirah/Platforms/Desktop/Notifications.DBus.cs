using Tmds.DBus.Protocol;

namespace Sefirah.Platforms.Desktop;

/// <summary>
/// Helper class for creating standard notification hints as defined in the Desktop Notifications Specification
/// See: https://specifications.freedesktop.org/notification-spec/1.3/hints.html
/// </summary>
public static class NotificationHints
{
    /// <summary>
    /// When set, a server that has the "action-icons" capability will attempt to interpret any action identifier as a named icon
    /// </summary>
    public static KeyValuePair<string, VariantValue> ActionIcons(bool enable) =>
        new("action-icons", VariantValue.Bool(enable));

    /// <summary>
    /// The type of notification this is
    /// </summary>
    public static KeyValuePair<string, VariantValue> Category(string category) =>
        new("category", VariantValue.String(category));

    /// <summary>
    /// Name of the desktop filename representing the calling program (e.g., "rhythmbox" from "rhythmbox.desktop")
    /// </summary>
    public static KeyValuePair<string, VariantValue> DesktopEntry(string desktopEntry) =>
        new("desktop-entry", VariantValue.String(desktopEntry));

    /// <summary>
    /// Alternative way to define the notification image path
    /// </summary>
    public static KeyValuePair<string, VariantValue> ImagePath(string imagePath) =>
        new("image-path", VariantValue.String(imagePath));


    /// <summary>
    /// When set the server will not automatically remove the notification when an action has been invoked
    /// </summary>
    public static KeyValuePair<string, VariantValue> Resident(bool resident) =>
        new("resident", VariantValue.Bool(resident));

    /// <summary>
    /// The path to a sound file to play when the notification pops up
    /// </summary>
    public static KeyValuePair<string, VariantValue> SoundFile(string soundFile) =>
        new("sound-file", VariantValue.String(soundFile));

    /// <summary>
    /// A themeable named sound from the freedesktop.org to play when the notification pops up (e.g., "message-new-instant" http://0pointer.de/public/sound-naming-spec.html)
    /// </summary>
    public static KeyValuePair<string, VariantValue> SoundName(string soundName) =>
        new("sound-name", VariantValue.String(soundName));

    /// <summary>
    /// Causes the server to suppress playing any sounds, if it has that ability
    /// </summary>
    public static KeyValuePair<string, VariantValue> SuppressSound(bool suppress) =>
        new("suppress-sound", VariantValue.Bool(suppress));

    /// <summary>
    /// When set the server will treat the notification as transient and by-pass the server's persistence capability
    /// </summary>
    public static KeyValuePair<string, VariantValue> Transient(bool transient) =>
        new("transient", VariantValue.Bool(transient));

    /// <summary>
    /// Specifies the X location on the screen that the notification should point to (must also specify Y)
    /// </summary>
    public static KeyValuePair<string, VariantValue> X(int x) =>
        new("x", VariantValue.Int32(x));

    /// <summary>
    /// Specifies the Y location on the screen that the notification should point to (must also specify X)
    /// </summary>
    public static KeyValuePair<string, VariantValue> Y(int y) =>
        new("y", VariantValue.Int32(y));

    /// <summary>
    /// The urgency level: 0=Low, 1=Normal, 2=Critical
    /// </summary>
    public static KeyValuePair<string, VariantValue> Urgency(byte urgency) =>
        new("urgency", VariantValue.Byte(urgency));

    // Convenience methods for urgency levels
    public static KeyValuePair<string, VariantValue> LowUrgency() => Urgency(0);
    public static KeyValuePair<string, VariantValue> NormalUrgency() => Urgency(1);
    public static KeyValuePair<string, VariantValue> CriticalUrgency() => Urgency(2);
}

/// <summary>
/// D-Bus client for org.freedesktop.Notifications
/// Implements the Desktop Notifications Specification
/// </summary>
partial class Notifications : NotificationsObject
{
    private const string __Interface = "org.freedesktop.Notifications";

    public Notifications(NotificationsService service, ObjectPath path) : base(service, path)
    { }

    /// <summary>
    /// Send a notification to the notification server
    /// </summary>
    /// <param name="appName">The name of the application sending the notification</param>
    /// <param name="replacesId">ID of notification to replace, or 0 for new notification</param>
    /// <param name="appIcon">Icon name or path for the application</param>
    /// <param name="summary">Brief summary text</param>
    /// <param name="body">Detailed notification body text</param>
    /// <param name="actions">Action pairs: action_id, display_text, action_id, display_text, ...</param>
    /// <param name="hints">Additional hints for the notification server</param>
    /// <param name="expireTimeout">Timeout in milliseconds, -1 for default, 0 for no timeout</param>
    /// <returns>Notification ID</returns>
    public Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body, string[] actions, Dictionary<string, VariantValue> hints, int expireTimeout)
    {
        return Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_u(m, (NotificationsObject)s!), this);
        MessageBuffer CreateMessage()
        {
            var writer = Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: __Interface,
                signature: "susssasa{sv}i",
                member: "Notify");
            writer.WriteString(appName);
            writer.WriteUInt32(replacesId);
            writer.WriteString(appIcon);
            writer.WriteString(summary);
            writer.WriteString(body);
            writer.WriteArray(actions);
            writer.WriteDictionary(hints);
            writer.WriteInt32(expireTimeout);
            return writer.CreateMessage();
        }
    }

    /// <summary>
    /// Force close a notification
    /// </summary>
    /// <param name="id">ID of the notification to close</param>
    public Task CloseNotificationAsync(uint id)
    {
        return Connection.CallMethodAsync(CreateMessage());
        MessageBuffer CreateMessage()
        {
            var writer = Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: __Interface,
                signature: "u",
                member: "CloseNotification");
            writer.WriteUInt32(id);
            return writer.CreateMessage();
        }
    }

    /// <summary>
    /// Get the capabilities supported by the notification server
    /// </summary>
    /// <returns>Array of capability strings</returns>
    public Task<string[]> GetCapabilitiesAsync()
    {
        return Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_as(m, (NotificationsObject)s!), this);
        MessageBuffer CreateMessage()
        {
            var writer = Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: __Interface,
                member: "GetCapabilities");
            return writer.CreateMessage();
        }
    }

    /// <summary>
    /// Get information about the notification server
    /// </summary>
    /// <returns>Tuple containing (name, vendor, version, spec_version)</returns>
    public Task<(string Name, string Vendor, string Version, string SpecVersion)> GetServerInformationAsync()
    {
        return Connection.CallMethodAsync(CreateMessage(), (Message m, object? s) => ReadMessage_ssss(m, (NotificationsObject)s!), this);
        MessageBuffer CreateMessage()
        {
            var writer = Connection.GetMessageWriter();
            writer.WriteMethodCallHeader(
                destination: Service.Destination,
                path: Path,
                @interface: __Interface,
                member: "GetServerInformation");
            return writer.CreateMessage();
        }
    }

    /// <summary>
    /// Watch for NotificationClosed signals
    /// </summary>
    /// <param name="handler">Handler for (id, reason) where reason: 1=expired, 2=dismissed, 3=closed by call, 4=undefined</param>
    public ValueTask<IDisposable> WatchNotificationClosedAsync(Action<Exception?, (uint Id, uint Reason)> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
        => base.WatchSignalAsync(Service.Destination, __Interface, Path, "NotificationClosed", (Message m, object? s) => ReadMessage_uu(m, (NotificationsObject)s!), handler, emitOnCapturedContext, flags);

    /// <summary>
    /// Watch for ActionInvoked signals
    /// </summary>
    /// <param name="handler">Handler for (id, action_key) when user clicks an action</param>
    public ValueTask<IDisposable> WatchActionInvokedAsync(Action<Exception?, (uint Id, string ActionKey)> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
        => base.WatchSignalAsync(Service.Destination, __Interface, Path, "ActionInvoked", (Message m, object? s) => ReadMessage_us(m, (NotificationsObject)s!), handler, emitOnCapturedContext, flags);

    /// <summary>
    /// Watch for ActivationToken signals
    /// </summary>
    /// <param name="handler">Handler for (id, activation_token) for window activation</param>
    public ValueTask<IDisposable> WatchActivationTokenAsync(Action<Exception?, (uint Id, string Token)> handler, bool emitOnCapturedContext = true, ObserverFlags flags = ObserverFlags.None)
        => base.WatchSignalAsync(Service.Destination, __Interface, Path, "ActivationToken", (Message m, object? s) => ReadMessage_us(m, (NotificationsObject)s!), handler, emitOnCapturedContext, flags);
}

/// <summary>
/// Service factory for creating Notifications objects
/// </summary>
partial class NotificationsService
{
    public Connection Connection { get; }
    public string Destination { get; }

    public NotificationsService(Connection connection, string destination)
        => (Connection, Destination) = (connection, destination);

    public Notifications CreateNotifications(ObjectPath path) => new Notifications(this, path);
}

/// <summary>
/// Base class for D-Bus notification objects
/// </summary>
class NotificationsObject
{
    public NotificationsService Service { get; }
    public ObjectPath Path { get; }
    protected Connection Connection => Service.Connection;

    protected NotificationsObject(NotificationsService service, ObjectPath path)
        => (Service, Path) = (service, path);

    // Property access methods
    protected MessageBuffer CreateGetPropertyMessage(string @interface, string property)
    {
        var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: Service.Destination,
            path: Path,
            @interface: "org.freedesktop.DBus.Properties",
            signature: "ss",
            member: "Get");
        writer.WriteString(@interface);
        writer.WriteString(property);
        return writer.CreateMessage();
    }

    protected MessageBuffer CreateGetAllPropertiesMessage(string @interface)
    {
        var writer = Connection.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: Service.Destination,
            path: Path,
            @interface: "org.freedesktop.DBus.Properties",
            signature: "s",
            member: "GetAll");
        writer.WriteString(@interface);
        return writer.CreateMessage();
    }

    // Signal watching methods
    protected ValueTask<IDisposable> WatchPropertiesChangedAsync<TProperties>(string @interface, MessageValueReader<PropertyChanges<TProperties>> reader, Action<Exception?, PropertyChanges<TProperties>> handler, bool emitOnCapturedContext, ObserverFlags flags)
    {
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Sender = Service.Destination,
            Path = Path,
            Interface = "org.freedesktop.DBus.Properties",
            Member = "PropertiesChanged",
            Arg0 = @interface
        };
        return Connection.AddMatchAsync(rule, reader,
                                                (Exception? ex, PropertyChanges<TProperties> changes, object? rs, object? hs) => ((Action<Exception?, PropertyChanges<TProperties>>)hs!).Invoke(ex, changes),
                                                this, handler, emitOnCapturedContext, flags);
    }

    public ValueTask<IDisposable> WatchSignalAsync<TArg>(string sender, string @interface, ObjectPath path, string signal, MessageValueReader<TArg> reader, Action<Exception?, TArg> handler, bool emitOnCapturedContext, ObserverFlags flags)
    {
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Sender = sender,
            Path = path,
            Member = signal,
            Interface = @interface
        };
        return Connection.AddMatchAsync(rule, reader,
                                                (Exception? ex, TArg arg, object? rs, object? hs) => ((Action<Exception?, TArg>)hs!).Invoke(ex, arg),
                                                this, handler, emitOnCapturedContext, flags);
    }

    public ValueTask<IDisposable> WatchSignalAsync(string sender, string @interface, ObjectPath path, string signal, Action<Exception?> handler, bool emitOnCapturedContext, ObserverFlags flags)
    {
        var rule = new MatchRule
        {
            Type = MessageType.Signal,
            Sender = sender,
            Path = path,
            Member = signal,
            Interface = @interface
        };
        return Connection.AddMatchAsync<object>(rule, (Message message, object? state) => null!,
                                                        (Exception? ex, object v, object? rs, object? hs) => ((Action<Exception?>)hs!).Invoke(ex), this, handler, emitOnCapturedContext, flags);
    }
    protected static uint ReadMessage_u(Message message, NotificationsObject _)
    {
        var reader = message.GetBodyReader();
        return reader.ReadUInt32();
    }

    protected static string[] ReadMessage_as(Message message, NotificationsObject _)
    {
        var reader = message.GetBodyReader();
        return reader.ReadArrayOfString();
    }

    protected static (string, string, string, string) ReadMessage_ssss(Message message, NotificationsObject _)
    {
        var reader = message.GetBodyReader();
        var arg0 = reader.ReadString();
        var arg1 = reader.ReadString();
        var arg2 = reader.ReadString();
        var arg3 = reader.ReadString();
        return (arg0, arg1, arg2, arg3);
    }

    protected static (uint, uint) ReadMessage_uu(Message message, NotificationsObject _)
    {
        var reader = message.GetBodyReader();
        var arg0 = reader.ReadUInt32();
        var arg1 = reader.ReadUInt32();
        return (arg0, arg1);
    }

    protected static (uint, string) ReadMessage_us(Message message, NotificationsObject _)
    {
        var reader = message.GetBodyReader();
        var arg0 = reader.ReadUInt32();
        var arg1 = reader.ReadString();
        return (arg0, arg1);
    }
}

/// <summary>
/// Property change event data
/// </summary>
class PropertyChanges<TProperties>
{
    public PropertyChanges(TProperties properties, string[] invalidated, string[] changed)
        => (Properties, Invalidated, Changed) = (properties, invalidated, changed);

    public TProperties Properties { get; }
    public string[] Invalidated { get; }
    public string[] Changed { get; }

    public bool HasChanged(string property) => Array.IndexOf(Changed, property) != -1;
    public bool IsInvalidated(string property) => Array.IndexOf(Invalidated, property) != -1;
}
