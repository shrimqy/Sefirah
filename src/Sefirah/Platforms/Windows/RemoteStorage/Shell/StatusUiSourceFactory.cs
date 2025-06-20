using Windows.Storage.Provider;

namespace Sefirah.Platforms.Windows.RemoteStorage.Shell;
public delegate IStorageProviderStatusUISource CreateStatusUiSource<T>(string syncRootId) where T : IStorageProviderStatusUISource;

/// <remarks>
/// <see href="https://learn.microsoft.com/en-us/uwp/api/windows.storage.provider.istorageproviderstatusuisourcefactory#windows-requirements">Windows 11 Insider Preview only</see>
/// </remarks>
public class StatusUiSourceFactory<T>
    : IStorageProviderStatusUISourceFactory
    where T : IStorageProviderStatusUISource, new()
{
    public IStorageProviderStatusUISource GetStatusUISource(string syncRootId)
    {
        return new T();
    }
}
