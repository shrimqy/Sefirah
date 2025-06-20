using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteStorage.RemoteAbstractions;
using System.Diagnostics.CodeAnalysis;

namespace Sefirah.Platforms.Windows.RemoteStorage.Worker.IO;
public static class RemoteDirectoryInfoExtensions
{
    public static int GetHashCode([DisallowNull] this RemoteDirectoryInfo obj) =>
        HashCode.Combine(
            // ignore sync attributes
            (int)obj.Attributes & ~SyncAttributes.ALL,
            obj.CreationTimeUtc,
            obj.LastWriteTimeUtc
        );
}
