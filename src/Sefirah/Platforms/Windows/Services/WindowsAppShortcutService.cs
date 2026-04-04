using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Windows.Management.Deployment;
using static Sefirah.Utils.IconUtils;
using RegisterPackageOptions = Windows.Management.Deployment.RegisterPackageOptions;

namespace Sefirah.Platforms.Windows.Services;

public sealed class WindowsAppShortcutService(ILogger logger) : IAppShortcutService
{
    private const string HostedAppsFolder = "HostedApps";
    /// <summary>Must match the HostRuntime Id in the host's Package.appxmanifest (windows.hostRuntime extension).</summary>
    private const string HostRuntimeId = "SefirahHost";
    private const string UnsignedPublisherOid = "OID.2.25.311729368913984317654407730594956997722=1";

    /// <summary>Prefix for the package parameter in the hosted app manifest</summary>
    public const string HostedPackageParamPrefix = "package:";
    private const string HostedPackageVersion = "1.0.0.0";
    private static readonly string HostedPublisherId = GetPublisherHash(UnsignedPublisherOid);

    private readonly PackageManager packageManager = new();
    private readonly string localStatePath = ApplicationData.Current.LocalFolder.Path;

    public async Task CreateAppShortcutAsync(ApplicationItem app)
    {
        var host = Package.Current.Id;
        var hostName = host.Name;
        var hostPublisher = host.Publisher;
        var hostVersion = $"{host.Version.Major}.{host.Version.Minor}.{host.Version.Build}.{host.Version.Revision}";

        var identityName = $"{hostName}.Hosted.{GetAppId(app.PackageName)}";
        var folderPath = Path.Combine(localStatePath, HostedAppsFolder, identityName);
        var imagesPath = Path.Combine(folderPath, "Images");

        try
        {
            Directory.CreateDirectory(imagesPath);

            await CopyHostedAppImagesAsync(app.PackageName, imagesPath);

            var manifestPath = Path.Combine(folderPath, "AppxManifest.xml");
            var manifestXml = BuildHostedAppManifest(identityName, app, hostName, hostPublisher, hostVersion);
            await File.WriteAllTextAsync(manifestPath, manifestXml);

            var manifestUri = new Uri(manifestPath);
            var options = new RegisterPackageOptions
            {
                AllowUnsigned = true
            };

            var result = await packageManager.RegisterPackageByUriAsync(manifestUri, options);

            if (!result.IsRegistered)
            {
                throw new Exception($"App registration failed: {result.ErrorText}");
            }
        }
        catch (Exception)
        {
            try { Directory.Delete(folderPath, true); } catch { }
            throw;
        }
    }

    public async Task RemoveAppShortcutAsync(string androidPackageName)
    {
        var hostName = Package.Current.Id.Name;
        var identityName = $"{hostName}.Hosted.{GetAppId(androidPackageName)}";
        var folderPath = Path.Combine(localStatePath, HostedAppsFolder, identityName);
        var fullName = GetPackageFullName(identityName);

        try
        {
            await packageManager.RemovePackageAsync(fullName);
        }
        catch (COMException) { }

        try 
        { 
            Directory.Delete(folderPath, true);
        } 
        catch (IOException) 
        {
            // shell may still have a handle to the icon
            throw;
        }
    }

    /// <summary>
    /// Uses <see cref="PackageManager.FindPackagesForUser"/> with the hosted app package family name (identity + publisher hash),
    /// then matches <see cref="PackageId.FullName"/> to the same string <see cref="RemovePackageAsync"/> uses—no LocalState folder check.
    /// </summary>
    /// <summary>
    /// Uses <see cref="PackageManager.FindPackagesForUser"/> with the hosted app package family name (identity + publisher hash),
    /// then matches <see cref="PackageId.FullName"/> to the same string <see cref="RemovePackageAsync"/> uses—no LocalState folder check.
    /// </summary>
    public bool IsShortcutRegistered(string androidPackageName)
    {
        var hostName = Package.Current.Id.Name;
        var identityName = $"{hostName}.Hosted.{GetAppId(androidPackageName)}";
        var expectedFullName = GetPackageFullName(identityName);
        var familyName = GetPackageFamilyName(identityName);

        try
        {
            foreach (var package in packageManager.FindPackagesForUser(string.Empty, familyName))
            {
                if (string.Equals(package.Id.FullName, expectedFullName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            var folderPath = Path.Combine(localStatePath, HostedAppsFolder, identityName);
            return Directory.Exists(folderPath);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "FindPackagesForUser failed for hosted package {FamilyName}", familyName);
            return false;
        }
    }

    private static string GetPackageFamilyName(string identityName) => $"{identityName}_{HostedPublisherId}";

    /// <summary>Builds the package full name deterministically (Name_Version_Architecture__PublisherId).</summary>
    private static string GetPackageFullName(string identityName) =>
        $"{identityName}_{HostedPackageVersion}_neutral__{HostedPublisherId}";

    /// <summary>Publisher hash for package full name (Crockford Base32 of first 8 bytes of SHA-256 of publisher).</summary>
    /// <remarks>From <see href="https://marcinotorowski.com/2021/12/19/calculating-hash-part-of-msix-package-family-name/">here</see>.</remarks>
    private static string GetPublisherHash(string publisher)
    {
        var encoded = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));
        var binaryString = string.Concat(encoded.Take(8).Select(c => Convert.ToString(c, 2).PadLeft(8, '0'))) + '0'; // 65 bits = 13 * 5
        var encodedPublisherId = string.Concat(Enumerable.Range(0, binaryString.Length / 5).Select(i => "0123456789abcdefghjkmnpqrstvwxyz".Substring(Convert.ToInt32(binaryString.Substring(i * 5, 5), 2), 1)));
        return encodedPublisherId;
    }

    /// <summary>Deterministic id from package name for Identity Name.</summary>
    private static string GetAppId(string packageName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(packageName));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string BuildHostedAppManifest(string identityName, ApplicationItem app, string hostName, string hostPublisher, string hostVersion)
    {
        var displayName = EscapeXml(app.AppName);

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"" xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"" xmlns:uap10=""http://schemas.microsoft.com/appx/manifest/uap/windows10/10"" xmlns:rescap=""http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"" IgnorableNamespaces=""uap rescap"">
  <Identity Name=""{identityName}"" Publisher=""{UnsignedPublisherOid}"" Version=""1.0.0.0""/>
  <Properties>
    <DisplayName>{displayName}</DisplayName>
    <PublisherDisplayName>Sefirah</PublisherDisplayName>
    <Logo>Images\StoreLogo.png</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Universal"" MinVersion=""10.0.19041.0"" MaxVersionTested=""10.0.26100.0""/>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.19041.0"" MaxVersionTested=""10.0.26100.0""/>
    <uap10:HostRuntimeDependency Name=""{hostName}"" Publisher=""{hostPublisher}"" MinVersion=""{hostVersion}""/>
  </Dependencies>
  <Resources>
    <Resource Language=""en-us"" />
  </Resources>
  <Applications>
    <Application Id=""HostedApp"" uap10:HostId=""{HostRuntimeId}"" uap10:Parameters=""{HostedPackageParamPrefix}{app.PackageName}"">
      <uap:VisualElements DisplayName=""{displayName}"" Description=""{app.PackageName}"" BackgroundColor=""transparent"" Square150x150Logo=""Images\Square150x150Logo.png"" Square44x44Logo=""Images\Square44x44Logo.png"">
        <uap:DefaultTile>
          <uap:ShowNameOnTiles>
            <uap:ShowOn Tile=""square150x150Logo""/>
          </uap:ShowNameOnTiles>
        </uap:DefaultTile>
      </uap:VisualElements>
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name=""runFullTrust""/>
  </Capabilities>
</Package>";
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private static async Task CopyHostedAppImagesAsync(string packageName, string imagesPath)
    {
        var sourcePath = GetAppIconFilePath(packageName);

        if (!File.Exists(sourcePath)) return;

        var dest44 = Path.Combine(imagesPath, "Square44x44Logo.png");

        await Task.Run(() => File.Copy(sourcePath, dest44, true));
    }
}
