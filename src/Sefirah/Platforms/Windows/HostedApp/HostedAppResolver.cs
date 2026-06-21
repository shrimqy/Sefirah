namespace Sefirah.Platforms.Windows.HostedApp;

/// <summary>
/// Resolves the host package's LocalState folder from a hosted app's install location.
/// Hosted processes run under an isolated package identity and cannot use ApplicationData.Current for host data.
/// </summary>
internal static class HostedAppResolver
{
    private const string HostedAppsFolderSegment = "HostedApps";
    private const string HostedPackageMarker = ".Hosted.";

    public static string? TryResolveHostLocalFolderPath()
    {
        var fromInstallPath = TryResolveFromInstalledLocation(Package.Current.InstalledLocation.Path);
        if (!string.IsNullOrEmpty(fromInstallPath))
            return fromInstallPath;

        return TryResolveFromHostedPackageName(Package.Current.Id.Name);
    }

    public static string? TryGetHostedManifestFolder(string hostLocalFolderPath, string hostedPackageName)
    {
        var manifestPath = Path.Combine(hostLocalFolderPath, HostedAppsFolderSegment, hostedPackageName, "AppxManifest.xml");
        return File.Exists(manifestPath) ? Path.GetDirectoryName(manifestPath) : null;
    }

    private static string? TryResolveFromInstalledLocation(string? installedPath)
    {
        if (string.IsNullOrEmpty(installedPath))
            return null;

        var normalized = installedPath.Replace('/', Path.DirectorySeparatorChar);
        var marker = $"{Path.DirectorySeparatorChar}{HostedAppsFolderSegment}{Path.DirectorySeparatorChar}";
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index >= 0 ? normalized[..index] : null;
    }

    private static string? TryResolveFromHostedPackageName(string packageName)
    {
        var hostedIndex = packageName.IndexOf(HostedPackageMarker, StringComparison.OrdinalIgnoreCase);
        if (hostedIndex < 0)
            return ApplicationData.Current.LocalFolder.Path;

        var hostBaseName = packageName[..hostedIndex];
        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages");

        if (!Directory.Exists(packagesRoot))
            return null;

        foreach (var dir in Directory.EnumerateDirectories(packagesRoot))
        {
            var folderName = Path.GetFileName(dir);
            if (!folderName.StartsWith(hostBaseName + "_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (folderName.Contains(HostedPackageMarker, StringComparison.OrdinalIgnoreCase))
                continue;

            return Path.Combine(dir, "LocalState");
        }

        return null;
    }
}
