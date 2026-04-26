using System.Security.Cryptography;
using System.Text;

namespace Sefirah.Platforms.Windows;

// Token + attestation for LimitedAccessFeatures.TryUnlockFeature on the PLT feature only.
// Algorithm: https://www.withinrafael.com/2021/01/04/generating-valid-tokens-to-access-limited-access-features-in-windows-10/
// Key for this feature id: https://gist.github.com/ADeltaX/285e017a1fefb0723b526246066b9f43

/// <summary>PLT-only LAF token/attestation for <see cref="LimitedAccessFeatures.TryUnlockFeature(string, string, string)"/>.</summary>
static class FeatureTokenGenerator
{
    public static string GenerateTokenFromFeatureId(string featureId, string featureKey)
        => GenerateFeatureToken(featureId, featureKey, AppInfo.Current.PackageFamilyName);

    public static string GenerateAttestation(string featureId)
        => $"{AppInfo.Current.PackageFamilyName.Split('_').Last()} has registered their use of {featureId} with Microsoft and agrees to the terms of use.";

    static string GenerateFeatureToken(string featureId, string featureKey, string packageIdentity)
    {
        var fullBytes = Encoding.UTF8.GetBytes($"{featureId}!{featureKey}!{packageIdentity}");
        var hash = SHA256.HashData(fullBytes);
        return Convert.ToBase64String(hash.AsSpan(0, 16));
    }
}
