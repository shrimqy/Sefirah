using System.Security.Cryptography;
using System.Text;
using Windows.Management.Deployment;

namespace Sefirah.Platforms.Windows.HostedApp;

/// <summary>
/// Deterministic Windows hosted-package identity derived from an Android package name.
/// </summary>
internal static class HostedPackageIdentity
{
    /// <summary>Application Id in the hosted app manifest (<c>Applications/Application@Id</c>).</summary>
    public const string ApplicationId = "HostedApp";

    /// <summary>Subfolder under the host app's LocalState where hosted packages are staged.</summary>
    public const string HostedAppsFolder = "HostedApps";

    private const string HostedPackageVersion = "1.0.0.0";

    /// <summary>Publisher OID used for unsigned hosted packages; must match the manifest and registration options.</summary>
    private const string UnsignedPublisherOid = "OID.2.25.311729368913984317654407730594956997722=1";

    private static readonly string PublisherHash = ComputePublisherHash(UnsignedPublisherOid);

    /// <summary>
    /// Builds the package identity name: <c>{hostName}.Hosted.{appId}</c>.
    /// Strips an existing <c>.Hosted.*</c> suffix from the host name when running inside a hosted package process.
    /// </summary>
    public static string GetIdentityName(string androidPackageName)
    {
        var hostName = Package.Current.Id.Name;
        var hostedIndex = hostName.IndexOf(".Hosted.", StringComparison.OrdinalIgnoreCase);
        if (hostedIndex >= 0)
            hostName = hostName[..hostedIndex];

        return $"{hostName}.Hosted.{GetAppId(androidPackageName)}";
    }

    /// <summary>Package family name: <c>{identityName}_{publisherHash}</c>.</summary>
    public static string GetPackageFamilyName(string identityName) => $"{identityName}_{PublisherHash}";

    /// <summary>Builds the package full name deterministically (<c>Name_Version_Architecture__PublisherId</c>).</summary>
    public static string GetPackageFullName(string identityName) =>
        $"{identityName}_{HostedPackageVersion}_neutral__{PublisherHash}";

    /// <summary>
    /// Application User Model ID for taskbar/shell grouping (<c>{packageFamilyName}!{ApplicationId}</c>),
    /// or <see langword="null"/> when the hosted package is not registered.
    /// </summary>
    public static string? GetAppUserModelId(string androidPackageName)
    {
        if (!IsRegistered(androidPackageName))
            return null;

        var identityName = GetIdentityName(androidPackageName);
        return $"{GetPackageFamilyName(identityName)}!{ApplicationId}";
    }

    /// <summary>
    /// Uses <see cref="PackageManager.FindPackagesForUser"/> with the hosted app package family name (identity + publisher hash),
    /// matching <see cref="PackageId.FullName"/> to the same string used by package registration and removal.
    /// Falls back to checking the staged folder under LocalState when <see cref="PackageManager"/> is unavailable.
    /// </summary>
    public static bool IsRegistered(string androidPackageName)
    {
        var identityName = GetIdentityName(androidPackageName);
        var expectedFullName = GetPackageFullName(identityName);
        var familyName = GetPackageFamilyName(identityName);

        try
        {
            var packageManager = new PackageManager();
            foreach (var package in packageManager.FindPackagesForUser(string.Empty, familyName))
            {
                if (string.Equals(package.Id.FullName, expectedFullName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch
        {
            var hostLocalFolder = HostedAppResolver.TryResolveHostLocalFolderPath()
                ?? ApplicationData.Current.LocalFolder.Path;
            return Directory.Exists(Path.Combine(hostLocalFolder, HostedAppsFolder, identityName));
        }
    }

    /// <summary>Deterministic id from Android package name for the manifest identity Name.</summary>
    private static string GetAppId(string packageName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(packageName));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    /// <summary>Publisher hash for package family name (Crockford Base32 of first 8 bytes of SHA-256 of publisher).</summary>
    /// <remarks>From <see href="https://marcinotorowski.com/2021/12/19/calculating-hash-part-of-msix-package-family-name/">here</see>.</remarks>
    private static string ComputePublisherHash(string publisher)
    {
        var encoded = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));
        var binaryString = string.Concat(encoded.Take(8).Select(c => Convert.ToString(c, 2).PadLeft(8, '0'))) + '0';
        return string.Concat(Enumerable.Range(0, binaryString.Length / 5)
            .Select(i => "0123456789abcdefghjkmnpqrstvwxyz"[Convert.ToInt32(binaryString.Substring(i * 5, 5), 2)]));
    }
}
