using Sefirah.Data.Contracts;
using Windows.Services.Store;
using WinRT.Interop;

namespace Sefirah.Platforms.Windows.Services;
public partial class WindowsUpdateService : ObservableObject, IUpdateService
{
    private StoreContext? storeContext;
    private List<StorePackageUpdate>? updatePackages = [];

    private bool isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => isUpdateAvailable;
        set => SetProperty(ref isUpdateAvailable, value);
    }

    public async Task CheckForUpdatesAsync()
    {
        await GetUpdatePackagesAsync();

        if (updatePackages is not null && updatePackages.Count > 0)
        {
            isUpdateAvailable = true;
        }
        isUpdateAvailable = false;
    }

    public async Task DownloadUpdatesAsync()
    {
        var downloadOperation = storeContext?.RequestDownloadAndInstallStorePackageUpdatesAsync(updatePackages);
        await downloadOperation.AsTask();            
    }

    private async Task GetUpdatePackagesAsync()
    {
        try
        {
            storeContext ??= await Task.Run(StoreContext.GetDefault);

            InitializeWithWindow.Initialize(storeContext, App.WindowHandle);

            var updateList = await storeContext.GetAppAndOptionalStorePackageUpdatesAsync();
            updatePackages = updateList?.ToList();
        }
        catch (Exception)
        {
            // GetAppAndOptionalStorePackageUpdatesAsync throws for unknown reasons.
        }
    }
}
