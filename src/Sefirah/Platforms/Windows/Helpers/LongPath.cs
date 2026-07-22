namespace Sefirah.Platforms.Windows.Helpers;

/// <summary>
/// Helpers for feeding paths to the raw Win32/CldApi calls (e.g. <c>CreateFile</c>,
/// <c>CfOpenFileWithOplock</c>), which - unlike .NET's managed file APIs - don't add the
/// extended-length ("\\?\") prefix themselves and fail with ERROR_PATH_NOT_FOUND (3) past MAX_PATH.
/// </summary>
public static class LongPath
{
    private const int MaxShortPath = 260;
    private const string ExtendedPrefix = @"\\?\";
    private const string UncExtendedPrefix = @"\\?\UNC\";

    /// <summary>
    /// Returns <paramref name="path"/> normalized and, when it exceeds MAX_PATH, prefixed with the
    /// extended-length form. Paths already in extended-length form are returned unchanged; otherwise
    /// <see cref="Path.GetFullPath(string)"/> is applied first to normalize separators (SFTP relative
    /// paths use '/') and resolve relative segments, both of which the extended-length form requires.
    /// </summary>
    public static string EnsureExtendedPrefix(string path)
    {
        if (path.StartsWith(ExtendedPrefix, StringComparison.Ordinal))
        {
            return path;
        }

        var fullPath = Path.GetFullPath(path);
        if (fullPath.Length < MaxShortPath)
        {
            return fullPath;
        }

        // \\server\share becomes \\?\UNC\server\share
        return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
            ? UncExtendedPrefix + fullPath[2..]
            : ExtendedPrefix + fullPath;
    }
}
