using Microsoft.Extensions.DependencyInjection;
using Sefirah.Data.Contracts;

namespace Sefirah.Platforms.Desktop;

/// <summary>
/// Extension methods for registering Desktop-specific services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Desktop-specific services to the service collection
    /// </summary>
    public static IServiceCollection AddDesktopServices(this IServiceCollection services)
    {
        // Register Desktop-specific notification services
        services.AddSingleton<IPlatformNotificationHandler, DesktopNotificationHandler>();

        return services;
    }
} 