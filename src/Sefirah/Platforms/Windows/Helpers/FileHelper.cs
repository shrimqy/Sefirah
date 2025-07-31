using System.Collections.Immutable;

namespace Sefirah.Platforms.Windows.Helpers;
public static class FileHelper
{
    private const int MAX_ATTEMPTS = 60;
    private static readonly ImmutableHashSet<int> EXPECTED_HRESULTS = [
		// ERROR_SHARING_VIOLATION The process cannot access the file because it is being used by another process.
		32
    ];
    private const int DELAY_MS = 500;
    private static readonly ImmutableHashSet<string> SYSTEM_FILE_NAMES = [
        "desktop.ini",
        "Thumbs.db"
    ];

    private static readonly ImmutableHashSet<string> SYSTEM_FOLDER_NAMES = [
        "/Android/obb",
        "/Android/data",
        "Android/obb",
        "Android/data",
    ];

    public static async Task WaitUntilUnlocked(Action funcOrAction, ILogger logger)
    {
        IOException? latestEx = null;
        for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
        {
            try
            {
                funcOrAction();
                return;
            }
            catch (IOException ex) when (IsExpectedHResult(ex.HResult))
            {
                latestEx = ex;
                await LogAndWait(ex, logger);
            }
        }
        throw latestEx ?? new Exception("Somehow finished attempts without latestEx");
    }

    public static async Task<TResult> WaitUntilUnlocked<TResult>(Func<TResult> funcOrAction, ILogger logger)
    {
        IOException? latestEx = null;
        for (var attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
        {
            try
            {
                return funcOrAction();
            }
            catch (IOException ex) when (IsExpectedHResult(ex.HResult))
            {
                latestEx = ex;
                await LogAndWait(ex, logger);
            }
        }
        throw latestEx ?? new Exception("Somehow finished attempts without latestEx");
    }

    private static bool IsExpectedHResult(int hresult) =>
        EXPECTED_HRESULTS.Contains(hresult & 0xFFFF);

    private static async Task LogAndWait(IOException ex, ILogger logger)
    {
        logger.LogWarning(ex, "File access error, waiting to retry; HR {HResult}", ex.HResult);
        await Task.Delay(DELAY_MS);
    }

    public static bool IsSystemFile(string path) =>
        SYSTEM_FILE_NAMES.Any(x => path.EndsWith(x, StringComparison.OrdinalIgnoreCase));

    public static bool IsSystemDirectory(string path) =>
        SYSTEM_FOLDER_NAMES.Any(x => path.StartsWith(x, StringComparison.OrdinalIgnoreCase));
}
