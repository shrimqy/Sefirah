using Sefirah.Platforms.Desktop.Tray.DBus;
using Tmds.DBus.Protocol;

namespace Sefirah.Platforms.Desktop.Tray;

internal sealed class LinuxStatusNotifierItemHandler : DBusHandler, IStatusNotifierItemHandler, IStatusNotifierItemProperties
{
    private const string StatusActive = "Active";
    private static readonly (int, int, byte[])[] EmptyPixmaps = [];

    private readonly ILogger logger;
    private readonly ObjectPath menuPath;
    private (int, int, byte[])[] iconPixmap = [(22, 22, CreateFallbackIcon(22))];

    public LinuxStatusNotifierItemHandler(
        DBusConnection connection,
        ILogger logger,
        LinuxTrayMenuHandler menuHandler,
        string iconPath)
        : base(connection, "/StatusNotifierItem", handlesChildPaths: false)
    {
        this.logger = logger;
        menuPath = menuHandler.Path;
        IconPath = iconPath;
    }

    public string IconPath { get; }

    public event EventHandler? Activated;

    string IStatusNotifierItemProperties.Category => "ApplicationStatus";
    string IStatusNotifierItemProperties.Id => "Sefirah";
    string IStatusNotifierItemProperties.Title => "Sefirah";
    string IStatusNotifierItemProperties.Status => StatusActive;
    int IStatusNotifierItemProperties.WindowId => 0;
    string IStatusNotifierItemProperties.IconThemePath => "";
    ObjectPath IStatusNotifierItemProperties.Menu => menuPath;
    bool IStatusNotifierItemProperties.ItemIsMenu => false;
    string IStatusNotifierItemProperties.IconName => "";
    (int, int, byte[])[] IStatusNotifierItemProperties.IconPixmap => iconPixmap;
    string IStatusNotifierItemProperties.OverlayIconName => "";
    (int, int, byte[])[] IStatusNotifierItemProperties.OverlayIconPixmap => EmptyPixmaps;
    string IStatusNotifierItemProperties.AttentionIconName => "";
    (int, int, byte[])[] IStatusNotifierItemProperties.AttentionIconPixmap => EmptyPixmaps;
    string IStatusNotifierItemProperties.AttentionMovieName => "";
    (string, (int, int, byte[])[], string, string) IStatusNotifierItemProperties.ToolTip => ("", EmptyPixmaps, "Sefirah", "");

    public void SetIcon((int Width, int Height, byte[] Pixels)[] pixmaps)
        => iconPixmap = pixmaps.Select(p => (p.Width, p.Height, p.Pixels)).ToArray();

    ValueTask IStatusNotifierItemHandler.HandleGetPropertyAsync(IStatusNotifierItemHandler.GetPropertyContext context)
        => context.Handle(this);

    ValueTask IStatusNotifierItemHandler.HandleGetAllPropertiesAsync(IStatusNotifierItemHandler.GetAllPropertiesContext context)
        => context.Handle(this);

    ValueTask IStatusNotifierItemHandler.ContextMenuAsync(int x, int y)
    {
        logger.Info("SNI ContextMenu");
        return default;
    }

    ValueTask IStatusNotifierItemHandler.ActivateAsync(int x, int y)
    {
        logger.Debug("SNI Activate");
        Activated?.Invoke(this, EventArgs.Empty);
        return default;
    }

    ValueTask IStatusNotifierItemHandler.SecondaryActivateAsync(int x, int y) => default;

    ValueTask IStatusNotifierItemHandler.ScrollAsync(int delta, string orientation) => default;

    private static byte[] CreateFallbackIcon(int size)
    {
        var pixels = new byte[size * size * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0xFF;
            pixels[i + 1] = 0x00;
            pixels[i + 2] = 0x00;
            pixels[i + 3] = 0xFF;
        }

        return pixels;
    }
}
