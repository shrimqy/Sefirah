using Sefirah;
using Sefirah.Platforms.Desktop.Tray.DBus;
using Tmds.DBus.Protocol;
using Uno.UI.Hosting;

internal class Program
{
    private const string InstanceServiceName = "com.castle.sefirah";
    private const string InstanceObjectPath = "/com/castle/sefirah";
    private static readonly TimeSpan DBusTimeout = TimeSpan.FromSeconds(2);

    [STAThread]
    public static void Main(string[] args)
    {
        DBusConnection? connection = null;
        InstanceHandler? handler = null;
        var shouldRedirect = false;

        var sessionAddress = DBusAddress.Session;
        if (sessionAddress is not null)
        {
            try
            {
                connection = new DBusConnection(sessionAddress);
                connection.ConnectAsync()
                    .AsTask()
                    .WaitAsync(DBusTimeout)
                    .GetAwaiter()
                    .GetResult();

                handler = new InstanceHandler(connection, () =>
                {
                    if (App.Current is App)
                        App.MainWindow.DispatcherQueue.TryEnqueue(App.ShowMainWindow);
                });
                connection.AddMethodHandler(handler);

                shouldRedirect = !connection
                    .TryRequestNameAsync(InstanceServiceName, default)
                    .WaitAsync(DBusTimeout)
                    .GetAwaiter()
                    .GetResult();
            }
            catch
            {
                if (handler is not null)
                    connection?.RemoveMethodHandler(handler.Path);

                connection?.Dispose();
                connection = null;
                handler = null;
            }
        }

        if (shouldRedirect && connection is not null)
        {
            if (handler is not null)
                connection.RemoveMethodHandler(handler.Path);

            try
            {
                MessageBuffer message;
                using (var writer = connection.GetMessageWriter())
                {
                    writer.WriteMethodCallHeader(
                        destination: InstanceServiceName,
                        path: InstanceObjectPath,
                        @interface: "com.castle.sefirah.SingleInstance",
                        signature: default,
                        member: "Activate");
                    message = writer.CreateMessage();
                }

                connection.CallMethodAsync(message)
                    .WaitAsync(DBusTimeout)
                    .GetAwaiter()
                    .GetResult();
            }
            finally
            {
                connection.Dispose();
            }

            return;
        }

        try
        {
            var host = UnoPlatformHostBuilder.Create()
                .App(() => new App())
                .UseX11()
                .UseLinuxFrameBuffer()
                .Build();

            host.Run();
        }
        finally
        {
            if (handler is not null)
                connection?.RemoveMethodHandler(handler.Path);

            connection?.Dispose();
        }
    }

    private sealed class InstanceHandler(DBusConnection connection, Action activated) : DBusHandler(connection, InstanceObjectPath, handlesChildPaths: false), ISingleInstanceHandler
    {
        ValueTask ISingleInstanceHandler.ActivateAsync()
        {
            activated();
            return default;
        }
    }
}
