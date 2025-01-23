using Sefirah.App.RemoteStorage.Helpers;
using Sefirah.App.RemoteStorage.RemoteAbstractions;
using System.Diagnostics.CodeAnalysis;

namespace Sefirah.App.RemoteStorage.Worker.IO;
public static class RemoteFileInfoExtensions
{
    public static int GetHashCode([DisallowNull] this RemoteFileInfo obj) =>
        HashCode.Combine(
            obj.Length,
            // ignore sync attributes
            (int)obj.Attributes & ~SyncAttributes.ALL,
            obj.CreationTimeUtc,
            obj.LastWriteTimeUtc
        );
}
