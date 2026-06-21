using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Windows.Interop;

/// <summary>
/// P/Invoke for <c>mrmsupport.dll</c> MRM indexing APIs, used to generate <c>Resources.pri</c> when pinning hosted apps.
/// </summary>
/// <remarks>
/// See <see href="https://learn.microsoft.com/en-us/windows/win32/menurc/pri-indexing-reference">PRI indexing reference</see>
/// and <see href="https://learn.microsoft.com/en-us/windows/uwp/app-resources/pri-apis-scenario-1">Scenario 1 walkthrough</see>.
/// </remarks>
internal static class MrmResourceIndexerInterop
{
    private const string MrmSupportDll = "mrmsupport.dll";
    private const uint CoInitMultithreaded = 0x0;
    private const int RpcEChangedMode = unchecked((int)0x80010106);

    private static readonly Lock LoadLock = new();
    private static bool nativeLibraryLoaded;

    [StructLayout(LayoutKind.Sequential)]
    private struct MrmResourceIndexerHandle
    {
        public IntPtr Handle;
    }

    internal enum MrmPlatformVersion : uint
    {
        Windows10_0_0_5 = 0x010A0005
    }

    internal enum MrmPackagingMode : uint
    {
        StandaloneFile = 0
    }

    internal enum MrmPackagingOptions : uint
    {
        None = 0
    }

    [DllImport(MrmSupportDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmCreateResourceIndexer(
        string packageFamilyName,
        string projectRoot,
        MrmPlatformVersion platformVersion,
        string defaultQualifiers,
        out MrmResourceIndexerHandle indexer);

    [DllImport(MrmSupportDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmIndexResourceContainerAutoQualifiers(
        MrmResourceIndexerHandle indexer,
        string containerPath);

    [DllImport(MrmSupportDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmIndexFileAutoQualifiers(
        MrmResourceIndexerHandle indexer,
        string filePath);

    [DllImport(MrmSupportDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmCreateResourceFile(
        MrmResourceIndexerHandle indexer,
        MrmPackagingMode packagingMode,
        MrmPackagingOptions packagingOptions,
        string outputDirectory);

    [DllImport(MrmSupportDll, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int MrmDestroyIndexerAndMessages(MrmResourceIndexerHandle indexer);

    [DllImport("ole32.dll")]
    private static extern int CoInitializeEx(IntPtr reserved, uint coInit);

    internal static void GenerateResourcesPri(
        string packageRoot,
        string packageFamilyName,
        IReadOnlyList<string> resourceContainerPaths,
        IReadOnlyList<string> assetRelativePaths)
    {
        EnsureNativeLibraryLoaded();
        EnsureComInitialized();

        MrmResourceIndexerHandle indexer = default;
        var created = false;
        try
        {
            ThrowOnFailure(MrmCreateResourceIndexer(
                packageFamilyName,
                packageRoot,
                MrmPlatformVersion.Windows10_0_0_5,
                "language-en-US_scale-100_contrast-standard",
                out indexer));
            created = true;

            foreach (var containerPath in resourceContainerPaths)
                ThrowOnFailure(MrmIndexResourceContainerAutoQualifiers(indexer, ToIndexerPath(containerPath)));

            foreach (var assetPath in assetRelativePaths)
                ThrowOnFailure(MrmIndexFileAutoQualifiers(indexer, ToIndexerPath(assetPath)));

            ThrowOnFailure(MrmCreateResourceFile(
                indexer,
                MrmPackagingMode.StandaloneFile,
                MrmPackagingOptions.None,
                packageRoot));
        }
        finally
        {
            if (created)
                MrmDestroyIndexerAndMessages(indexer);
        }
    }

    private static string ToIndexerPath(string path) => path.Replace('/', '\\');

    private static void EnsureNativeLibraryLoaded()
    {
        if (nativeLibraryLoaded)
            return;

        lock (LoadLock)
        {
            if (nativeLibraryLoaded)
                return;

            if (NativeLibrary.TryLoad(MrmSupportDll, typeof(MrmResourceIndexerInterop).Assembly, DllImportSearchPath.ApplicationDirectory, out _))
            {
                nativeLibraryLoaded = true;
                return;
            }

            var candidatePath = Path.Combine(AppContext.BaseDirectory, MrmSupportDll);
            if (File.Exists(candidatePath))
            {
                NativeLibrary.Load(candidatePath);
                nativeLibraryLoaded = true;
                return;
            }

            throw new DllNotFoundException($"Could not load {MrmSupportDll} from {AppContext.BaseDirectory}.");
        }
    }

    private static void EnsureComInitialized()
    {
        var hr = CoInitializeEx(IntPtr.Zero, CoInitMultithreaded);
        if (hr is < 0 and not RpcEChangedMode)
            Marshal.ThrowExceptionForHR(hr);
    }

    private static void ThrowOnFailure(int hr)
    {
        if (hr < 0)
            Marshal.ThrowExceptionForHR(hr);
    }
}
