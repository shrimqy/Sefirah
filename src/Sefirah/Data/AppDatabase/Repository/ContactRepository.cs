using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.Models;
using Sefirah.Utils;
using System.Collections.Concurrent;

namespace Sefirah.Data.AppDatabase.Repository;

public class ContactRepository(DatabaseContext context, ILogger<ContactRepository> logger)
{
    private readonly ConcurrentDictionary<string, Contact> contactsById = new(StringComparer.Ordinal);

    public IEnumerable<Contact> Contacts => contactsById.Values;

    public async Task<List<ContactEntity>> GetAllContactsAsync()
    {
        return await Task.Run(() =>
        {
            var contacts = context.Database.Table<ContactEntity>().ToList();
            return contacts.GroupBy(c => c.Number).Select(group => group.First()).ToList();
        });
    }

    public async Task<List<ContactEntity>> GetContactsForDevice(string deviceId)
    {
        return await Task.Run(() => context.Database.Table<ContactEntity>()
            .Where(c => c.DeviceId == deviceId)
            .OrderByDescending(n => n.DisplayName)
            .ToList());
    }

    /// <summary>First contact row per phone number for a device (e.g. call log / message display resolution).</summary>
    public async Task<Dictionary<string, ContactEntity>> GetContactLookupByNumberAsync(string deviceId)
    {
        return await Task.Run(() =>
            context.Database.Table<ContactEntity>()
                .Where(c => c.DeviceId == deviceId)
                .ToList()
                .GroupBy(c => c.Number)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal));
    }

    public async Task<ContactEntity?> GetContactAsync(string deviceId, string phoneNumber)
    {
        return await Task.Run(() => context.Database.Table<ContactEntity>().FirstOrDefault(c => c.DeviceId == deviceId && c.Number == phoneNumber));
    }

    public async Task<ContactEntity?> GetContactByPhoneNumberAsync(string phoneNumber)
    {
        var contact = GetContactByPhoneNumber(phoneNumber);
        if (contact is null)
        {
            return null;
        }

        return await Task.Run(() => context.Database.Find<ContactEntity>(contact.Id));
    }

    public Contact? GetContactByPhoneNumber(string phoneNumber)
    {
        var trimmed = phoneNumber?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        // 1) Exact (formatted) match first.
        var exact = Contacts.FirstOrDefault(c => string.Equals(c.Address?.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        return Contacts.FirstOrDefault(c => PhoneNumberUtils.IsSemanticMatch(trimmed, c.Address));
    }

    public CallerContact? GetCallerContactByPhoneNumber(string phoneNumber)
    {
        var contact = GetContactByPhoneNumber(phoneNumber);
        if (contact is null)
        {
            return null;
        }

        return new CallerContact(contact.Address, contact.DisplayName, contact.Avatar);
    }

    public async Task SaveContactAsync(string deviceId, ContactInfo contactInfo)
    {
        if (string.IsNullOrWhiteSpace(contactInfo.Id)) return;
        if (contactsById.ContainsKey(contactInfo.Id)) return;

        var contactEntity = contactInfo.ToEntity(deviceId);
        await Task.Run(() => context.Database.InsertOrReplace(contactEntity));

        contactsById[contactInfo.Id] = await App.MainWindow.DispatcherQueue.EnqueueAsync(() => contactEntity.ToContact());
    }

    public async Task LoadContacts()
    {
        var contacts = await GetAllContactsAsync();
        var models = await Task.WhenAll(contacts.Select(c => c.ToContact()));

        contactsById.Clear();
        foreach (var contact in models)
        {
            contactsById[contact.Id] = contact;
        }
    }

    public List<Contact> SearchContacts(string searchText, int take = 10)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [];
        }

        return Contacts
            .Where(c =>
                (c.DisplayName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                c.Address.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.DisplayName)
            .Take(take)
            .ToList();
    }

    /// <summary>
    /// Deletes all stored contacts for a device. Used when clearing device-scoped data (e.g. SMS wipe or device removal).
    /// </summary>
    public async void DeleteAllContactsForDevice(string deviceId)
    {
        try
        {
            context.Database.Table<ContactEntity>().Where(c => c.DeviceId == deviceId).Delete();
            await LoadContacts();
            logger.LogInformation("Deleted all contacts for device {DeviceId}", deviceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete contacts for device {DeviceId}", deviceId);
        }
    }
}
