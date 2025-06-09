namespace Sefirah.Platforms.Windows.Helpers;
public static class PathMapper
{
    public static string GetRelativePath(string fullPath, string startPath)
    {
        // Normalize paths to use the same directory separator
        fullPath = fullPath.Replace('\\', '/').TrimEnd('/');
        startPath = startPath.Replace('\\', '/').TrimEnd('/');

        if (fullPath.Length == startPath.Length)
        {
            return string.Empty;
        }

        // Ensure we have a proper path separator after startPath
        if (!fullPath.StartsWith(startPath + "/"))
        {
            return fullPath[startPath.Length..].TrimStart('/');
        }

        return fullPath[(startPath.Length + 1)..];
    }

    public static string ReplaceStart(string source, string oldStart, string newStart)
    {
        source = source.Replace('\\', '/');
        oldStart = oldStart.Replace('\\', '/');
        newStart = newStart.Replace('\\', '/');

        return string.Concat(newStart, source[oldStart.Length..]);
    }

    public static void EnsureSubDirectoriesExist(string path)
    {
        var directory = Path.GetDirectoryName(path)!;
        if (Path.Exists(directory))
        {
            return;
        }
        Directory.CreateDirectory(directory);
    }

    public static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimEnd('/');
    }
}
