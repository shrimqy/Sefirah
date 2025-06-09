using Sefirah.Platforms.Windows.Helpers;
using Sefirah.Platforms.Windows.RemoteAbstractions;
using System.Diagnostics.CodeAnalysis;

namespace Sefirah.Platforms.Windows.Worker.IO;
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
