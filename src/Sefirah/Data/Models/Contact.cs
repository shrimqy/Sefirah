using Sefirah.Helpers;
using Sefirah.Utils;
using Windows.Storage.Streams;

namespace Sefirah.Data.Models;

/// <summary>
/// Contact info for a single phone number (lookup result), not the full person record.
/// Multiple numbers for the same person share a <see cref="ContactKey"/> but are separate instances.
/// </summary>
public class Contact
{
    public Contact(string address, string? displayName = null)
    {
        Address = address;
        DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : address;
    }

    internal Contact(string address, string displayName, bool hasAvatar, IRandomAccessStreamReference? avatarStream)
    {
        Address = address;
        DisplayName = !string.IsNullOrEmpty(displayName) ? displayName : address;
        HasAvatar = hasAvatar;
        AvatarStream = avatarStream;
    }

    public string Address { get; }

    internal string? ContactKey { get; init; }

    public string DisplayName { get; set; }

    public bool HasAvatar { get; private set; }

    public IRandomAccessStreamReference? AvatarStream { get; private set; }

    public string PlaceholderColorHex => ContactHelper.GetPlaceholderColorHex(Address);

    public string Initials => ContactHelper.GetInitials(DisplayName);

    public bool HasInitials => !string.IsNullOrEmpty(Initials);

    public Task<Uri?> GetToastAvatarUriAsync() =>
        ImageHelper.SaveStreamToTemporaryFileAsync(AvatarStream);

    internal void UpdateAvatar(bool hasAvatar, IRandomAccessStreamReference? avatarStream)
    {
        HasAvatar = hasAvatar;
        AvatarStream = avatarStream;
    }
}
