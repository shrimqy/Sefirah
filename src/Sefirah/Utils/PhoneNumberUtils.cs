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
