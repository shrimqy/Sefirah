using System.Text.Json;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Data.Models.Messages;
using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class ConversationEntity
{
    [PrimaryKey]
    public long ThreadId { get; set; }

    [Indexed]
    public string DeviceId { get; set; } = string.Empty;

    public string? AddressesJson { get; set; }

    public long LastMessageTimestamp { get; set; }

    public string? LastMessage { get; set; }

    public bool HasRead { get; set; }

    public long TimeStamp { get; set; }

    [Ignore]
    public List<string> Addresses { get; set; } = [];

    #region Helpers
    public static ConversationEntity FromMessage(ConversationInfo thread, string deviceId)
    {
        var latestMessage = thread.Messages.OrderByDescending(m => m.Timestamp).First();
        return new ConversationEntity
        {
            ThreadId = thread.ThreadId,
            DeviceId = deviceId,
            AddressesJson = JsonSerializer.Serialize(thread.Recipients),
            HasRead = thread.Messages.Any(m => m.Read),
            TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastMessageTimestamp = latestMessage.Timestamp,
            LastMessage = latestMessage.Body
        };
    }

    internal async Task<Conversation> ToConversationAsync(ContactRepository contactRepository)
    {
        if (!string.IsNullOrEmpty(AddressesJson))
        {
            Addresses = JsonSerializer.Deserialize<List<string>>(AddressesJson) ?? [];
        }

        var contacts = new List<ParticipantInfo>();
        foreach (var address in Addresses)
        {
            var contact = contactRepository.GetContactByPhoneNumber(address);
            if (contact is not null)
            {
                contacts.Add(new ParticipantInfo(contact.Address, contact.DisplayName, contact.Avatar));
            }
            else
            {
                contacts.Add(new ParticipantInfo(address, address));
            }
        }

        string avatarGlyph = string.Empty;
        if (contacts.Count == 1 && contacts[0].Avatar is null)
        {
            avatarGlyph = "\uE77B";
        }
        else if (contacts.Count > 1)
        {
            avatarGlyph = "\uE716";
        }
        var avatarImage = contacts.Count > 0 ? contacts[0].Avatar : null;

        return new Conversation
        {
            ThreadId = ThreadId,
            Contacts = contacts,
            LastMessage = LastMessage ?? string.Empty,
            LastMessageTimestamp = LastMessageTimestamp,
            HasRead = HasRead,
            AvatarGlyph = avatarGlyph,
            AvatarImage = avatarImage,
        };
    }
    #endregion
}
