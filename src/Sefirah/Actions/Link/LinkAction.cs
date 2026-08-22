using Sefirah.Utils;
using Windows.System;

namespace Sefirah.Actions.Link;

public sealed class LinkSettings
{
    public string Url { get; set; } = string.Empty;
}

public sealed class LinkAction(ActionItem item) : IAction, IActionSettings
{
    public static ActionMetadata Metadata { get; } = new("Link", "Link");

    public string DefaultIcon => Metadata.DefaultIcon;

    public bool IsValid => Uri.TryCreate(item.Get<LinkSettings>().Url, UriKind.Absolute, out _);

    public UIElement CreateSettingPanel() => new LinkActionSettings(item);

    public async Task ExecuteAsync()
    {
        var url = item.Get<LinkSettings>().Url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid URL: '{url}'");
        }

        await Launcher.LaunchUriAsync(uri);
    }
}
