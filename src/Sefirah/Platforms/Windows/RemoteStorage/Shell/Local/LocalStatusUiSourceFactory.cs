using System.Runtime.InteropServices;
using Sefirah.Platforms.Windows.RemoteStorage.Shell;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell.Local;
[ComVisible(true), Guid("d3252227-1396-40a5-bfe9-1fcc49333ab3")]
public class LocalStatusUiSourceFactory
    : StatusUiSourceFactory<LocalStatusUiSource>
{ }
