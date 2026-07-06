using System.Globalization;
using System.Text.RegularExpressions;
using PhoneNumbers;

namespace Sefirah.Utils;

public static partial class PhoneNumberUtils
{
    private static readonly PhoneNumberUtil phoneNumberUtil = PhoneNumberUtil.GetInstance();
    private static readonly Regex NonPhoneCharactersRegex = PhoneNumberRegex();

    private static readonly string currentRegionInfo = RegionInfo.CurrentRegion.TwoLetterISORegionName;

    public static bool IsSemanticMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        var leftParsed = TryParse(left);
        if (leftParsed is null)
        {
            return false;
        }

        var rightParsed = TryParse(right);
        if (rightParsed is null)
        {
            return false;
        }

        var matchType = phoneNumberUtil.IsNumberMatch(leftParsed, rightParsed);
        return matchType is PhoneNumberUtil.MatchType.EXACT_MATCH or PhoneNumberUtil.MatchType.NSN_MATCH;
    }

    /// <summary>
    /// Whether two address strings refer to the same phone number (case-insensitive exact, then libphonenumber semantic).
    /// </summary>
    public static bool IsMatch(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
        || IsSemanticMatch(left, right);

    private static PhoneNumber? TryParse(string value)
    {
        var normalizedInput = value.Trim();
        if (!LooksLikePhoneNumber(normalizedInput))
        {
            return null;
        }

        try
        {
            return phoneNumberUtil.Parse(normalizedInput, currentRegionInfo);
        }
        catch (NumberParseException)
        {
            return null;
        }
    }

    public static bool IsPhoneNumber(string? value) =>
        !string.IsNullOrWhiteSpace(value) && LooksLikePhoneNumber(value.Trim());

    /// <summary>
    /// Canonical form for storage and cache keys (E.164 when parseable, otherwise trimmed input).
    /// </summary>
    public static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        var parsed = TryParse(trimmed);
        return parsed is not null
            ? phoneNumberUtil.Format(parsed, PhoneNumberFormat.E164)
            : trimmed;
    }

    private static bool LooksLikePhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (NonPhoneCharactersRegex.IsMatch(value))
        {
            return false;
        }

        var digitCount = value.Count(char.IsDigit);
        return digitCount is >= 3 and <= 20;
    }

    [GeneratedRegex(@"[^\d\+\-\(\)\.\s]", RegexOptions.Compiled)]
    private static partial Regex PhoneNumberRegex();
}
