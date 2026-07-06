using System.Collections.Concurrent;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.Data.AppDatabase.Repository;

public class ContactRepository(DatabaseContext context, ILogger<ContactRepository> logger)
{
    private const string ContactTable = nameof(ContactEntity);
    private const string AvatarColumn = nameof(ContactEntity.Avatar);

    // Synced contacts: deviceId -> (normalized number -> Contact for that number).
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, Contact>> caches = new(StringComparer.Ordinal);

    // temp cache for unknown numbers and addresses
    private readonly ConcurrentDictionary<string, Contact> tempCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Finds a known contact on the given device
    /// </summary>
    public Contact? LookupContact(string deviceId, string address)
    {
        var normalizedAddress = PhoneNumberUtils.Normalize(address);
        if (string.IsNullOrEmpty(normalizedAddress))
            return null;

        return ResolveContact(deviceId, normalizedAddress);
    }

    /// <summary>
    /// Resolves a contact for display on a device. Returns synced/temp when known; otherwise creates a temp placeholder.
    /// </summary>
    public Contact GetContact(string? deviceId, string address, string? displayName = null)
    {
        if (string.IsNullOrEmpty(deviceId))
            return new Contact(address);

        var normalizedAddress = PhoneNumberUtils.Normalize(address);
        var known = ResolveContact(deviceId, normalizedAddress);
        if (known is not null)
            return known;

        var contact = new Contact(address, displayName);
        tempCache[normalizedAddress] = contact;
        return contact;
    }

    public Task<Contact> SaveContactAsync(string deviceId, ContactInfo contactInfo) =>
        Task.Run(() =>
        {
            var (entity, displayNumber, normalizedAddress) = UpsertContact(deviceId, contactInfo);
            tempCache.TryRemove(normalizedAddress, out _);
            return CacheContact(entity, displayNumber, normalizedAddress);
        });

    public Task LoadContacts() =>
        Task.Run(() =>
        {
            const string sql =
                "SELECT c.rowid AS RowId, c.Key AS ContactKey, c.DeviceId, c.DisplayName, length(c.Avatar) AS AvatarLen, p.Number " +
                "FROM ContactEntity c " +
                "INNER JOIN PhoneNumberEntity p ON p.ContactKey = c.Key";

            foreach (var row in context.Database.Query<ContactRow>(sql))
            {
                var contact = CreateContact(row.ContactKey, row.Number, row.DisplayName, row.AvatarLen > 0, row.RowId);
                var cacheKey = PhoneNumberUtils.Normalize(row.Number);
                if (!string.IsNullOrEmpty(cacheKey))
                    GetOrCreateDeviceCache(row.DeviceId)[cacheKey] = contact;
            }
        });

    public List<Contact> SearchContacts(string deviceId, string searchText, int take = 5)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return [];

        var localResults = FilterContacts(GetContactsForDevice(deviceId), searchText);
        if (localResults.Count > 0)
            return [.. localResults.Take(take)];

        return [.. FilterContacts(GetContactsFromOtherDevices(deviceId), searchText).Take(take)];
    }

    public void DeleteAllContactsForDevice(string deviceId)
    {
        try
        {
            context.Database.Execute(
                $"DELETE FROM {nameof(PhoneNumberEntity)} WHERE ContactKey IN (" +
                $"SELECT Key FROM {ContactTable} WHERE DeviceId = ?)",
                deviceId);
            context.Database.Execute($"DELETE FROM {ContactTable} WHERE DeviceId = ?", deviceId);

            caches.TryRemove(deviceId, out _);

            logger.Info($"Deleted all contacts for device {deviceId}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to delete contacts for device {deviceId}", ex);
        }
    }

    private Contact? ResolveContact(string deviceId, string normalizedAddress) =>
        TryGetFromDeviceCache(deviceId, normalizedAddress)
        ?? (tempCache.TryGetValue(normalizedAddress, out var temp) ? temp : null);

    private Contact? TryGetFromDeviceCache(string deviceId, string normalizedAddress)
    {
        if (!caches.TryGetValue(deviceId, out var cache))
            return null;

        if (cache.TryGetValue(normalizedAddress, out var exact))
            return exact;

        foreach (var contact in cache.Values)
        {
            if (PhoneNumberUtils.IsMatch(normalizedAddress, contact.Address))
                return contact;
        }

        return null;
    }

    private IEnumerable<Contact> GetContactsForDevice(string deviceId) =>
        caches.TryGetValue(deviceId, out var cache) ? cache.Values.DistinctBy(c => c.ContactKey ?? c.Address) : [];

    private IEnumerable<Contact> GetContactsFromOtherDevices(string deviceId) =>
        caches
            .Where(kvp => !string.Equals(kvp.Key, deviceId, StringComparison.Ordinal))
            .SelectMany(kvp => kvp.Value.Values)
            .DistinctBy(c => c.ContactKey ?? c.Address);

    private static List<Contact> FilterContacts(IEnumerable<Contact> contacts, string searchText)
    {
        return contacts
            .Where(c =>
                (c.DisplayName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (!string.IsNullOrEmpty(c.Address) &&
                 c.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase)))
            .DistinctBy(c => c.ContactKey ?? c.Address)
            .OrderBy(c => c.DisplayName)
            .ToList();
    }

    private (ContactEntity Entity, string DisplayNumber, string NormalizedAddress) UpsertContact(string deviceId, ContactInfo contactInfo)
    {
        var entity = ContactEntity.FromMessage(contactInfo, deviceId);
        var displayNumber = contactInfo.Number.Trim();
        var normalizedAddress = PhoneNumberUtils.Normalize(contactInfo.Number);

        context.Database.InsertOrUpdate(entity);

        context.Database.InsertOrUpdate(new PhoneNumberEntity
        {
            Key = PhoneNumberEntity.GetKey(entity.Key, normalizedAddress),
            ContactKey = entity.Key,
            Number = displayNumber,
        });

        return (entity, displayNumber, normalizedAddress);
    }

    private ConcurrentDictionary<string, Contact> GetOrCreateDeviceCache(string deviceId) =>
        caches.GetOrAdd(deviceId, _ => new ConcurrentDictionary<string, Contact>(StringComparer.Ordinal));

    private Contact CreateContact(string contactKey, string number, string displayName, bool hasAvatar, long rowId)
    {
        var stream = hasAvatar
            ? new SqliteBlobStreamReference(context.Database, ContactTable, AvatarColumn, rowId)
            : null;

        return new Contact(number, displayName, hasAvatar, stream)
        {
            ContactKey = contactKey,
        };
    }

    private Contact CacheContact(ContactEntity entity, string displayNumber, string canonical)
    {
        var hasAvatar = entity.Avatar is { Length: > 0 };
        var rowId = hasAvatar ? context.Database.GetRowId(entity) : 0;
        var contact = CreateContact(entity.Key, displayNumber, entity.DisplayName, hasAvatar, rowId);

        GetOrCreateDeviceCache(entity.DeviceId)[canonical] = contact;

        var avatarStream = contact.AvatarStream;

        _ = Task.Run(() =>
        {
            foreach (var cache in caches.Values)
            {
                foreach (var cached in cache.Values)
                {
                    if (cached.ContactKey != entity.Key)
                        continue;

                    cached.DisplayName = entity.DisplayName;
                    cached.UpdateAvatar(hasAvatar, avatarStream);
                }
            }
        });

        return contact;
    }
    private sealed record ContactRow
    {
        public long RowId { get; set; }

        public string ContactKey { get; set; } = string.Empty;

        public string DeviceId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public long AvatarLen { get; set; }

        public string Number { get; set; } = string.Empty;
    }
}
