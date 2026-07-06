namespace Sefirah.Utils;

public static class ContactHelper
{
    private static readonly string[] PlaceholderColors =
    [
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
        "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9",
        "#F8C471", "#82E0AA", "#F1948A", "#85C1E9", "#D7BDE2",
        "#A9DFBF", "#F9E79F", "#AED6F1", "#FADBD8", "#D5DBDB"
    ];

    private static readonly char[] InitialSeparator = [' ', '\t', '\n', '\r'];

    public static string GetPlaceholderColorHex(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return PlaceholderColors[0];
        }

        var hash = StringComparer.Ordinal.GetHashCode(key);
        var index = (int)((uint)hash % (uint)PlaceholderColors.Length);
        return PlaceholderColors[index];
    }

    public static string GetInitials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || PhoneNumberUtils.IsPhoneNumber(displayName))
        {
            return string.Empty;
        }

        var words = displayName.Trim().Split(InitialSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return string.Empty;
        }

        if (words.Length == 1)
        {
            return words[0][..1].ToUpperInvariant();
        }

        return string.Concat(words[0].AsSpan(0, 1), words[1].AsSpan(0, 1)).ToUpperInvariant();
    }
}
