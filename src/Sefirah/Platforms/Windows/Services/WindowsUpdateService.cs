using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Sefirah.Data.Contracts;
using Windows.Services.Store;
using WinRT.Interop;
using static Sefirah.Constants;

namespace Sefirah.Platforms.Windows.Services;
public partial class WindowsUpdateService : ObservableObject, IUpdateService
{
    private const string UpdateNotificationTag = "app-update";
    private const string UpdateNotificationGroup = "update";

    private StoreContext? storeContext;
    private List<StorePackageUpdate>? updatePackages = [];

    private bool isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => isUpdateAvailable;
        set => SetProperty(ref isUpdateAvailable, value);
    }

    private bool isUpdating;
    public bool IsUpdating
    {
        get => isUpdating;
        private set => SetProperty(ref isUpdating, value);
    }

    public bool IsMandatory => updatePackages?.Where(e => e.Mandatory).ToList().Count >= 1;

    public async Task CheckForUpdatesAsync()
    {
        await GetUpdatePackagesAsync();

        if (updatePackages is not null && updatePackages.Count > 0)
        {
            IsUpdateAvailable = true;
            ShowUpdateAvailableNotification();
            return;
        }
        IsUpdateAvailable = false;
    }

    private static void ShowUpdateAvailableNotification()
    {
        try
        {
            var builder = new AppNotificationBuilder()
                .AddText("UpdateNotification.Title".GetLocalizedResource())
                .AddText("UpdateNotification.Subtitle".GetLocalizedResource())
                .SetTag(UpdateNotificationTag)
                .SetGroup(UpdateNotificationGroup)
                .AddButton(new AppNotificationButton("UpdateNotification.Action".GetLocalizedResource())
                    .AddArgument("notificationType", ToastNotificationType.Update)
                    .AddArgument("action", "download"));

            var notification = builder.BuildNotification();
            notification.ExpiresOnReboot = true;
            AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Notification may fail if not registered; ignore
        }
    }

    public async Task DownloadUpdatesAsync()
    {
        if (updatePackages is null || updatePackages.Count == 0)
            return;

        IsUpdating = true;
        try
        {
            var downloadOperation = storeContext?.RequestDownloadAndInstallStorePackageUpdatesAsync(updatePackages);
            var result = await downloadOperation.AsTask();

            if (result?.OverallState == StorePackageUpdateState.Completed)
            {
                IsUpdateAvailable = false;
                updatePackages.Clear();
            }
        }
        finally
        {
            IsUpdating = false;
        }
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
