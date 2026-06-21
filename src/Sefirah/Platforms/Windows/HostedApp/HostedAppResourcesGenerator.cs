using System.Runtime.InteropServices;
using Sefirah.Platforms.Windows.Interop;

namespace Sefirah.Platforms.Windows.HostedApp;

/// <summary>
/// Generates <c>Resources.pri</c> for hosted app packages via bundled <c>mrmsupport.dll</c>.
/// </summary>
internal static class HostedAppResourcesGenerator
{
    private const string ResourcesResw = """
        <?xml version="1.0"?>
        <root>
        	<data name="PublisherDisplayName">
        		<value></value>
        	</data>
        </root>
        """;

    public static Task GeneratePriAsync(string packageRoot, string identityName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(packageRoot);
        ArgumentException.ThrowIfNullOrEmpty(identityName);

        return Task.Run(() => GeneratePri(packageRoot, identityName), cancellationToken);
    }

    private static void GeneratePri(string packageRoot, string identityName)
    {
        var manifestPath = Path.Combine(packageRoot, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Hosted app manifest is required before generating Resources.pri.", manifestPath);

        var resourcesEnUsPath = Path.Combine(packageRoot, "Resources", "en-US");
        Directory.CreateDirectory(resourcesEnUsPath);
        File.WriteAllText(Path.Combine(resourcesEnUsPath, "Resources.resw"), ResourcesResw);

        var resourceContainers = new[] { @"Resources\en-US\Resources.resw" };

        var imagesPath = Path.Combine(packageRoot, "Images");
        var assetPaths = Directory.Exists(imagesPath)
            ? Directory.EnumerateFiles(imagesPath, "*.png", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(packageRoot, file))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        var packageFamilyName = HostedPackageIdentity.GetPackageFamilyName(identityName);

        try
        {
            MrmResourceIndexerInterop.GenerateResourcesPri(packageRoot, packageFamilyName, resourceContainers, assetPaths);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            throw new InvalidOperationException("MRM resource indexing (mrmsupport.dll) is not available.", ex);
        }
        catch (COMException ex)
        {
            throw new InvalidOperationException($"Failed to generate Resources.pri for hosted app {identityName} (0x{ex.HResult:X8}).", ex);
        }

        EnsurePriFileName(packageRoot);
    }

    private static void EnsurePriFileName(string packageRoot)
    {
        var expectedPath = Path.Combine(packageRoot, "Resources.pri");
        if (File.Exists(expectedPath))
            return;

        var lowercasePath = Path.Combine(packageRoot, "resources.pri");
        if (File.Exists(lowercasePath))
            File.Move(lowercasePath, expectedPath);
        else
            throw new InvalidOperationException("MRM did not produce Resources.pri for the hosted app package.");
    }
}
