using Microsoft.Extensions.Hosting;
using Sefirah.Common.Utils;
using Serilog;
using Windows.ApplicationModel;

namespace Sefirah.App.Helpers;


/// <summary>
/// Provides static helper to manage app lifecycle.
/// </summary>
public static class AppLifecycleHelper
{
    internal static void CloseApp()
    {
        MainWindow.Instance.Close();
    }

    /// <summary>
    /// Gets application package version.
    /// </summary>
    public static Version AppVersion { get; } =
        new(Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);


    /// <summary>
    /// Initializes the app components.
    /// </summary>
    public static async Task InitializeAppComponentsAsync()
    {

    }


    /// <summary>
    /// Configures DI (dependency injection) container.
    /// </summary>
    public static IHost ConfigureHost()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) => services
                // Logging
                .AddSingleton<Sefirah.Common.Utils.ILogger>(new SerilogWrapperLogger(Log.Logger))
            ).Build();
    }

    /// <summary>
    /// Shows exception on the Debug Output.
    /// </summary>
    public static void HandleAppUnhandledException(Exception? ex)
    {
        var logger = Ioc.Default.GetRequiredService<Common.Utils.ILogger>();
        if (ex is not null)
        {
            logger.Error("Unhandled exception occurred", ex);
        }
    }
}
