using SQLite;

namespace Sefirah.Data.AppDatabase.Models;

public class PhoneNumberEntity
{
    [PrimaryKey]
    public string Key { get; set; } = string.Empty;

    [Indexed]
    public string ContactKey { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public static string GetKey(string contactKey, string number) => $"{contactKey}:{number}";
}
