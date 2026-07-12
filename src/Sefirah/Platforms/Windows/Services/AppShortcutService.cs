using System.Runtime.InteropServices;
using Sefirah.Data.Models;
using Sefirah.Platforms.Windows.HostedApp;
using Windows.Management.Deployment;
using Sefirah.Utils;
using RegisterPackageOptions = Windows.Management.Deployment.RegisterPackageOptions;

namespace Sefirah.Platforms.Windows.Services;

public sealed class AppShortcutService(ILogger logger) : IAppShortcutService
{
    private const int HResultPackageNotFound = unchecked((int)0x80073CF1);

    private const string HostedAppsFolder = HostedPackageIdentity.HostedAppsFolder;
    /// <summary>Must match the HostRuntime Id in the host's Package.appxmanifest (windows.hostRuntime extension).</summary>
    private const string HostRuntimeId = "SefirahHost";
    private const string UnsignedPublisherOid = "OID.2.25.311729368913984317654407730594956997722=1";

    /// <summary>Prefix for the package parameter in the hosted app manifest</summary>
    public const string HostedPackageParamPrefix = "package:";

    private readonly PackageManager packageManager = new();
    private readonly string localStatePath = ApplicationData.Current.LocalFolder.Path;

    public async Task CreateAppShortcutAsync(ApplicationItem app)
    {
        var host = Package.Current.Id;
        var hostName = host.Name;
        var hostPublisher = host.Publisher;
        var hostVersion = $"{host.Version.Major}.{host.Version.Minor}.{host.Version.Build}.{host.Version.Revision}";

        var identityName = HostedPackageIdentity.GetIdentityName(app.PackageName);
        var folderPath = Path.Combine(localStatePath, HostedAppsFolder, identityName);
        var imagesPath = Path.Combine(folderPath, "Images");
        var fullName = HostedPackageIdentity.GetPackageFullName(identityName);

        try
        {
            await TryRemoveHostedPackageAsync(identityName);

            if (Directory.Exists(folderPath))
            {
                try { Directory.Delete(folderPath, true); }
                catch (IOException ex)
                {
                    logger.Debug($"Could not fully clear hosted app folder {identityName} before recreate", ex);
                }
            }

            Directory.CreateDirectory(imagesPath);

            await CopyHostedAppImagesAsync(app.DeviceId, app.PackageName, folderPath);

            var manifestPath = Path.Combine(folderPath, "AppxManifest.xml");
            var manifestXml = BuildHostedAppManifest(identityName, app, hostName, hostPublisher, hostVersion);
            await File.WriteAllTextAsync(manifestPath, manifestXml);

            await HostedAppResourcesGenerator.GeneratePriAsync(folderPath, identityName);

            var manifestUri = new Uri(manifestPath);
            var options = new RegisterPackageOptions
            {
                AllowUnsigned = true
            };

            DeploymentResult result;
            try
            {
                result = await packageManager.RegisterPackageByUriAsync(manifestUri, options);
            }
            catch (COMException ex)
            {
                throw new InvalidOperationException($"App registration failed (0x{ex.HResult:X8}): {ex.Message}", ex);
            }

            if (!result.IsRegistered)
            {
                throw new InvalidOperationException($"App registration failed: {result.ErrorText}");
            }

            logger.Info($"Registered hosted app package {fullName}");
        }
        catch (Exception ex)
        {
            logger.Error($"Failed to create hosted app shortcut for {app.PackageName}", ex);
            throw;
        }
    }

    public async Task RemoveAppShortcutAsync(string androidPackageName)
    {
        var identityName = HostedPackageIdentity.GetIdentityName(androidPackageName);
        var folderPath = Path.Combine(localStatePath, HostedAppsFolder, identityName);

        try
        {
            await TryRemoveHostedPackageAsync(identityName);
        }
        catch (Exception ex)
        {
            logger.Debug($"RemovePackageAsync failed for {identityName}", ex);
        }

        TryDeleteHostedAppFolder(folderPath, identityName);
    }

    public bool IsShortcutRegistered(string androidPackageName) =>
        HostedPackageIdentity.IsRegistered(androidPackageName);

    private async Task TryRemoveHostedPackageAsync(string identityName)
    {
        if (!IsHostedPackageInstalled(identityName))
            return;

        var fullName = HostedPackageIdentity.GetPackageFullName(identityName);

        try
        {
            var result = await packageManager.RemovePackageAsync(fullName);
            if (IsHostedPackageInstalled(identityName))
                logger.Debug($"Hosted package still installed after RemovePackageAsync ({fullName}): {result.ErrorText}");
        }
        catch (COMException ex) when (ex.HResult == HResultPackageNotFound)
        {
        }
    }

    private void TryDeleteHostedAppFolder(string folderPath, string identityName)
    {
        if (!Directory.Exists(folderPath))
            return;

        try
        {
            Directory.Delete(folderPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.Debug($"Could not delete hosted app folder {identityName} (files may be in use)", ex);
        }
    }

    /// <summary>
    /// Uses <see cref="PackageManager.FindPackagesForUser"/> with the hosted app package family name,
    /// matching <see cref="PackageId.FullName"/> to the same string <see cref="PackageManager.RemovePackageAsync"/> uses.
    /// </summary>
    private bool IsHostedPackageInstalled(string identityName)
    {
        var expectedFullName = HostedPackageIdentity.GetPackageFullName(identityName);
        var familyName = HostedPackageIdentity.GetPackageFamilyName(identityName);

        foreach (var package in packageManager.FindPackagesForUser(string.Empty, familyName))
        {
            if (string.Equals(package.Id.FullName, expectedFullName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string BuildHostedAppManifest(string identityName, ApplicationItem app, string hostName, string hostPublisher, string hostVersion)
    {
        var displayName = EscapeXml(app.AppName);

        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"" xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"" xmlns:uap10=""http://schemas.microsoft.com/appx/manifest/uap/windows10/10"" xmlns:rescap=""http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"" IgnorableNamespaces=""uap uap10 rescap"">
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

    private static async Task CopyHostedAppImagesAsync(string deviceId, string packageName, string packageRoot)
    {
        var sourcePath = LocalAppPaths.GetAppIconFilePath(deviceId, packageName);
        await HostedAppIconGenerator.GenerateAsync(sourcePath, packageRoot);
    }
}
