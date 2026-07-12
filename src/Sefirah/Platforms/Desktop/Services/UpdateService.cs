namespace Sefirah.Platforms.Desktop.Services;

public partial class UpdateService : ObservableObject, IUpdateService
{
    public bool IsUpdateAvailable => false;
    public bool IsUpdating => false;

    public Task CheckForUpdatesAsync()
    {
        return Task.CompletedTask;
    }

    public Task DownloadUpdatesAsync()
    {
        return Task.CompletedTask;
    }
}
