using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using Sefirah.Helpers;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ContactEntity
{
    [PrimaryKey]
    public string Id { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public string? LookupKey { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public byte[]? Avatar { get; set; }

    #region Helpers
    public static ContactEntity FromMessage(ContactInfo message, string deviceId) => new()
    {
        Id = message.Id,
        DeviceId = deviceId,
        LookupKey = message.LookupKey,
        DisplayName = message.DisplayName,
        Number = message.Number,
        Avatar = message.PhotoBase64 is not null ? Convert.FromBase64String(message.PhotoBase64) : null
    };

    internal async Task<Contact> ToContact()
    {
        var displayName = !string.IsNullOrEmpty(DisplayName) ? DisplayName : Number;
        return new Contact(Number, displayName, Avatar is not null ? await Avatar.ToBitmapAsync() : null);
    }
    #endregion
}
