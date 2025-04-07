using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Renci.SshNet;
using Sefirah.App.RemoteStorage.Abstractions;
using Sefirah.App.RemoteStorage.Configuration;
using Sefirah.App.RemoteStorage.Remote;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using Sefirah.App.RemoteStorage.RemoteSftp;
using Sefirah.App.RemoteStorage.Shell;
using Sefirah.App.RemoteStorage.Shell.Commands;
using Sefirah.App.RemoteStorage.Shell.Local;
using Sefirah.App.RemoteStorage.Worker;
using Sefirah.App.RemoteStorage.Worker.IO;
using System.Threading.Channels;

namespace Sefirah.App.RemoteStorage;
public static class ServiceCollectionExtensions
{
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
