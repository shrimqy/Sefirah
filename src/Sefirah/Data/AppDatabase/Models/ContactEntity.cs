using Sefirah.Data.Models;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ContactEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public string ContactId { get; set; } = string.Empty;

    public string LookupKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public byte[]? Avatar { get; set; }

    #region Helpers
    public static string GetKey(string deviceId, string contactId) => $"{deviceId}:{contactId}";

    public static ContactEntity FromMessage(ContactInfo message, string deviceId) 
    
    {
        byte[]? avatar = null;
        try
        {
            avatar = Convert.FromBase64String(message.PhotoBase64);
        }
        catch (Exception) { }

        return new ContactEntity
        {
            Key = GetKey(deviceId, message.Id),
            DeviceId = deviceId,
            ContactId = message.Id,
            LookupKey = message.LookupKey,
            DisplayName = message.DisplayName,
            Avatar = avatar,
        };
    }
    #endregion
}
