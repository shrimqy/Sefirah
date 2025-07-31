using Microsoft.Extensions.DependencyInjection;
using Sefirah.Data.Contracts;
using Sefirah.Platforms.Desktop.Services;

namespace Sefirah.Platforms.Desktop;

/// <summary>
/// Extension methods for registering Desktop-specific services
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        services.AddSingleton<IPlatformNotificationHandler, DesktopNotificationHandler>();
        services.AddSingleton<IPlaybackService, DesktopPlaybackService>();
        services.AddSingleton<IActionService, DesktopActionService>();
        services.AddSingleton<IUpdateService, DesktopUpdateService>();
        services.AddSingleton<ISftpService, DesktopSftpService>();
        return services;
    }
} 
