namespace Sefirah.Extensions;

public static class FeatureServiceCollectionExtensions
{
    /// <summary>
    /// Registers a feature as its contract and as <see cref="IFeature"/>.
    /// </summary>
    public static IServiceCollection AddFeature<TService, TImplementation>(this IServiceCollection services)
        where TService : class, IFeature
        where TImplementation : class, TService
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>());
        services.AddSingleton<IFeature>(sp => sp.GetRequiredService<TImplementation>());
        return services;
    }

    /// <summary>
    /// Registers a feature whose contract does not inherit <see cref="IFeature"/>.
    /// </summary>
    public static IServiceCollection AddFeatureWithContract<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService, IFeature
    {
        services.AddSingleton<TImplementation>();
        services.AddSingleton<TService>(sp => sp.GetRequiredService<TImplementation>());
        services.AddSingleton<IFeature>(sp => sp.GetRequiredService<TImplementation>());
        return services;
    }
}
