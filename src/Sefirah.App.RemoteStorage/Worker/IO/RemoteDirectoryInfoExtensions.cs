using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using System.Diagnostics.CodeAnalysis;

namespace Sefirah.App.RemoteStorage.Worker.IO;
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
