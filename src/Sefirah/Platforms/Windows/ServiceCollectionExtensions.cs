using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Renci.SshNet;
using Sefirah.Data.Contracts;
using Sefirah.Platforms.Windows.RemoteStorage.Abstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Configuration;
using Sefirah.Platforms.Windows.RemoteStorage.Remote;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using Sefirah.Platforms.Windows.RemoteStorage.Sftp;
using Sefirah.Platforms.Windows.RemoteStorage.Shell;
using Sefirah.Platforms.Windows.RemoteStorage.Shell.Commands;
using Sefirah.Platforms.Windows.RemoteStorage.Shell.Local;
using Sefirah.Platforms.Windows.RemoteStorage.Worker;
using Sefirah.Platforms.Windows.RemoteStorage.Worker.IO;
using Sefirah.Platforms.Windows.Services;

namespace Sefirah.Platforms.Windows;

/// <summary>
/// Extension methods for registering Windows-specific services
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsServices(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformNotificationHandler, WindowsNotificationHandler>();
        services.AddSingleton<IPlaybackService, WindowsPlaybackService>();
        services.AddSingleton<IActionService, WindowsActionService>();
        services.AddSingleton<IUpdateService, WindowsUpdateService>();

        // Remote Storage
        services.AddSftpRemoteServices();
        services.AddCloudSyncWorker();

        // Shell
        services.AddCommonClassObjects();
        services.AddSingleton<ShellRegistrar>();
        services.AddHostedService<ShellWorker>();

        services.AddSingleton<SyncProviderWorker>();
        services.AddSingleton<ISftpService, WindowsSftpService>();
        return services;
    }

    public static IServiceCollection AddRemoteFactories(this IServiceCollection services) =>
    services
        .AddScoped<RemoteReadServiceFactory>()
        .AddScoped((sp) => sp.GetRequiredService<RemoteReadServiceFactory>().Create())
        .AddScoped<RemoteReadWriteServiceFactory>()
        .AddScoped((sp) => sp.GetRequiredService<RemoteReadWriteServiceFactory>().Create())
        .AddScoped<RemoteWatcherFactory>()
        .AddScoped((sp) => sp.GetRequiredService<RemoteWatcherFactory>().Create());

    public static IServiceCollection AddClassObject<T>(this IServiceCollection services) where T : class =>
    services
        .AddTransient<T>()
        .AddSingleton<ClassFactory<T>.Generator>((sp) => () => sp.GetRequiredService<T>())
        .AddSingleton<IClassFactoryOf, ClassFactory<T>>();

    public static IServiceCollection AddCommonClassObjects(this IServiceCollection services) =>
        services
            .AddClassObject<SyncCommand>()
            .AddClassObject<UploadCommand>();

    public static IServiceCollection AddLocalClassObjects(this IServiceCollection services) =>
        services
            .AddClassObject<LocalThumbnailProvider>()
            .AddTransient<LocalStatusUiSource>()
            .AddSingleton<CreateStatusUiSource<LocalStatusUiSource>>((sp) => (syncRootId) => sp.GetRequiredService<LocalStatusUiSource>())
            .AddClassObject<LocalStatusUiSourceFactory>();

    public static IServiceCollection AddCloudSyncWorker(this IServiceCollection services) =>
        services
            .AddOptionsWithValidateOnStart<ProviderOptions>()
            .Configure<IConfiguration>((options, config) => {
                options.ProviderId = "Shrimqy:Sefirah";
            })
            .Services
            .AddSingleton<SyncProviderPool>()
            .AddSingleton<SyncProviderContextAccessor>()
            .AddSingleton<ISyncProviderContextAccessor>((sp) => sp.GetRequiredService<SyncProviderContextAccessor>())

            .AddSingleton((sp) =>
                Channel.CreateUnbounded<ShellCommand>(
                    new UnboundedChannelOptions
                    {
                        SingleReader = false,
                    }
                )
            )
        .AddSingleton((sp) => sp.GetRequiredService<Channel<ShellCommand>>().Reader)
        .AddSingleton((sp) => sp.GetRequiredService<Channel<ShellCommand>>().Writer)
        .AddScoped<ShellCommandQueue>()

            // Sync Provider services
            .AddRemoteFactories()
            .AddScoped<FileLocker>()
            .AddScoped((sp) =>
                Channel.CreateUnbounded<Func<Task>>(
                    new UnboundedChannelOptions
                    {
                        SingleReader = true,
                    }
                )
            )
            .AddScoped((sp) => sp.GetRequiredService<Channel<Func<Task>>>().Reader)
            .AddScoped((sp) => sp.GetRequiredService<Channel<Func<Task>>>().Writer)
            .AddScoped<TaskQueue>()
            .AddScoped<SyncProvider>()
            .AddScoped<SyncRootConnector>()
            .AddScoped<SyncRootRegistrar>()
            .AddScoped<PlaceholdersService>()
            .AddScoped<ClientWatcher>()
            .AddScoped<RemoteWatcher>();

    public static IServiceCollection AddSftpRemoteServices(this IServiceCollection services) =>
        services
            .AddSingleton<SftpContextAccessor>()
            .AddKeyedSingleton<IRemoteContextSetter>("sftp", (sp, key) => sp.GetRequiredService<SftpContextAccessor>())
            .AddSingleton((sp) => sp.GetRequiredKeyedService<IRemoteContextSetter>("sftp"))
            .AddSingleton<ISftpContextAccessor>((sp) => sp.GetRequiredService<SftpContextAccessor>())
            .AddScoped((sp) => {
                var context = sp.GetRequiredService<SyncProviderContextAccessor>();
                var contextAccessor = sp.GetRequiredService<ISftpContextAccessor>();
                var client = new SftpClient(
                    contextAccessor.Context.Host,
                    contextAccessor.Context.Port,
                    contextAccessor.Context.Username,
                    contextAccessor.Context.Password
                );
                try
                {
                    client.Connect();
                }
                catch
                {
                    // ignore
                }
                return client;
            })
            .AddKeyedScoped<IRemoteReadWriteService, SftpReadWriteService>("sftp")
            .AddScoped((sp) => new LazyRemote<IRemoteReadWriteService>(() => sp.GetRequiredKeyedService<IRemoteReadWriteService>("sftp"), SftpConstants.KIND))
            .AddKeyedScoped<IRemoteReadService>("sftp", (sp, key) => sp.GetRequiredService<IRemoteReadWriteService>())
            .AddScoped((sp) => new LazyRemote<IRemoteReadService>(() => sp.GetRequiredKeyedService<IRemoteReadService>("sftp"), SftpConstants.KIND))
            .AddKeyedScoped<IRemoteWatcher, SftpWatcher>("sftp")
            .AddScoped((sp) => new LazyRemote<IRemoteWatcher>(() => sp.GetRequiredKeyedService<IRemoteWatcher>("sftp"), SftpConstants.KIND));
} 
