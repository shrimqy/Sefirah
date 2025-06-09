namespace Sefirah.Data.Items;

/// <summary>
/// Represents an item for open source library shown on <see cref="Views.Settings.AboutPage"/>.
/// </summary>
/// <remarks>
/// Initializes an instance of <see cref="OpenSourceLibraryItem"/> class.
/// </remarks>
/// <param name="url">The URL</param>
/// <param name="name">The name</param>
public class OpenSourceLibraryItem(string url, string name)
{
    /// <summary>
    /// Gets the URL that navigates to the open source library.
    /// </summary>
    public string Url { get; } = url;

    /// <summary>
    /// Gets the name of the open source library.
    /// </summary>
    public string Name { get; } = name;
}
