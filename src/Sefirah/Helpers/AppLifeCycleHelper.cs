using Sefirah.Models;
using Serilog;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sefirah.Helpers;

/// <summary>
/// Provides static helper to manage app lifecycle.
/// </summary>
public static class AppLifeCycleHelper
{
    /// <summary>
    /// Gets application package version.
    /// </summary>
    public static Version AppVersion { get; } =
        new(Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);

    public static async Task InitializeAppComponentsAsync()
    {
    }

    public static IApplicationBuilder ConfigureApp(this App app, LaunchActivatedEventArgs args)
    {
        return app.CreateBuilder(args)
            .Configure(host => host
#if DEBUG
                // Switch to Development environment when running in DEBUG
                .UseEnvironment(Environments.Development)
#endif
                .UseLogging(configure: (context, logBuilder) =>
                {
                    // Configure log levels for different categories of logging
                    logBuilder
                        .SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                                LogLevel.Debug :
                                LogLevel.Warning)

                        // Default filters for core Uno Platform namespaces
                        .CoreLogLevel(LogLevel.Warning);

                }, enableUnoLogging: true)
                .UseSerilog(
                    consoleLoggingEnabled: true,
                    fileLoggingEnabled: true,
                    configureLogger: config =>
                    {
                        config.WriteTo.File(
                            Path.Combine(ApplicationData.Current.LocalFolder.Path, "Logs", "Log_.log"),
                            rollingInterval: RollingInterval.Day,
                            retainedFileCountLimit: 7
                        );
                    }
                )
                .UseConfiguration(configure: configBuilder =>
                    configBuilder
                        .EmbeddedSource<App>()
                        .Section<AppConfig>()
                )
                // Enable localization (see appsettings.json for supported languages)
                .UseLocalization()
                .ConfigureServices((context, services) => services

                // Add plain ILogger for BaseViewModel
                .AddSingleton<ILogger>(sp => sp.GetRequiredService<ILogger<App>>())
                )
            );
    }

    public static async Task HandleStartupTaskAsync(bool enable)
    {
#if WINDOWS
        var startupTask = await StartupTask.GetAsync("8B5D3E3F-9B69-4E8A-A9F7-BFCA793B9AF0");

        if (enable)
        {
            if (startupTask.State == StartupTaskState.Disabled)
                await startupTask.RequestEnableAsync();
        }
        else
        {
            if (startupTask.State == StartupTaskState.Enabled)
                startupTask.Disable();
        }
#endif
    }
}
