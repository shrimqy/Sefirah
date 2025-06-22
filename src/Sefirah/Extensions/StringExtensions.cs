using System.Collections.Concurrent;
using Microsoft.Windows.ApplicationModel.Resources;

namespace Sefirah.Extensions;

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
        //if (cachedResources.TryGetValue(resourceKey, out var value))
        //{
        //    return value;
        //}
        
        // Get the string localizer when needed instead of static initialization
        IStringLocalizer? stringLocalizer = null;
        try
        {
            stringLocalizer = Ioc.Default.GetService<IStringLocalizer>();
        }
        catch
        {
            // Fallback if service is not available yet
        }
        
        string? value = null;
        if (stringLocalizer != null)
        {
            value = stringLocalizer[resourceKey];
        }
        
        // Fallback to resource map if stringLocalizer failed
        if (string.IsNullOrEmpty(value))
        {
            value = resourcesTree?.TryGetValue(resourceKey)?.ValueAsString;
        }

        return value ?? string.Empty;
    }
}
