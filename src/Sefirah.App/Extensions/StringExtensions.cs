using Microsoft.Windows.ApplicationModel.Resources;
using System.Collections.Concurrent;

namespace Sefirah.App.Extensions;

/// <summary>
/// Extension methods for working with localized resources and message formatting.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Resource map for accessing localized strings.
    /// It is initialized with the main resource map of the application's resources and the subtree "Resources".
    /// </summary>
    private static readonly ResourceMap resourcesTree = new ResourceManager().MainResourceMap.TryGetSubtree("Resources");

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
        value = resourcesTree?.TryGetValue(resourceKey)?.ValueAsString;
        return cachedResources[resourceKey] = value ?? string.Empty;
    }
}
