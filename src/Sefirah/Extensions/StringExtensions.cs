using System.Collections.Concurrent;

namespace Sefirah.Extensions;

/// <summary>
/// Extension methods for working with localized resources and message formatting.
/// </summary>
public static class StringExtensions
{
    private static readonly IStringLocalizer stringLocalizer = Ioc.Default.GetRequiredService<IStringLocalizer>();

    private static readonly ConcurrentDictionary<string, string> cachedResources = new();

    /// <summary>
    /// Retrieves a localized resource string from the resource map.
    /// </summary>
    /// <param name="resourceKey">The key for the resource string.</param>
    /// <returns>The localized resource string.</returns>
    public static string GetLocalizedResource(this string resourceKey)
    {
        if (cachedResources.TryGetValue(resourceKey, out var value))
        {
            return value;
        }

        if (string.IsNullOrEmpty(value))
        {
            value = stringLocalizer[resourceKey];
        }

        return value ?? string.Empty;
    }
}
