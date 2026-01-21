using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using static Sefirah.Utils.IconUtils;

namespace Sefirah.ViewModels;
public sealed partial class AppsViewModel : BaseViewModel
{
    #region Services
    private RemoteAppRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private IAdbService AdbService { get; } = Ioc.Default.GetRequiredService<IAdbService>();
    #endregion

    #region Properties
    public ObservableCollection<ApplicationInfo> Apps { get; set; } = [];
    public ObservableCollection<ApplicationInfo> PinnedApps { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    public bool IsEmpty => !Apps.Any() && !IsLoading;
    public bool HasPinnedApps => PinnedApps.Any();

    #endregion

    #region Commands

    [RelayCommand]
    public void RefreshApps()
    {
        Apps.Clear();
        PinnedApps.Clear();
        OnPropertyChanged(nameof(HasPinnedApps));

        if (DeviceManager.ActiveDevice is null) return;
        IsLoading = true;
        var message = new CommandMessage { CommandType = CommandType.RequestAppList };
        DeviceManager.ActiveDevice.SendMessage(message);
    }

    public void PinApp(ApplicationInfo app)
    {
        try
        {
            if (app.DeviceInfo.Pinned)
            {
                app.DeviceInfo.Pinned = false;
                RemoteAppsRepository.UnpinApp(app, DeviceManager.ActiveDevice!.Id);
                PinnedApps.Remove(app);
            }
            else
            {
                app.DeviceInfo.Pinned = true;
                RemoteAppsRepository.PinApp(app, DeviceManager.ActiveDevice!.Id);
                PinnedApps.Add(app);
            }
            OnPropertyChanged(nameof(HasPinnedApps));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error pinning app: {AppPackage}", app?.PackageName);
        }
    }

    public async void UninstallApp(ApplicationInfo app)
    {
        try
        {
            await AdbService.UninstallApp(DeviceManager.ActiveDevice!.Id, app.PackageName);
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                Apps.Remove(app);
                PinnedApps.Remove(app);
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasPinnedApps));
            });
            await RemoteAppsRepository.RemoveDeviceFromApplication(app.PackageName, DeviceManager.ActiveDevice!.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uninstalling app: {AppPackage}", app.PackageName);
        }
    }

    #endregion

    #region Methods

    private async void LoadApps()
    {
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(async() =>
            {
                Apps.Clear();
                PinnedApps.Clear();

                if (DeviceManager.ActiveDevice is null) return;

                IsLoading = true;
                Apps = RemoteAppsRepository.GetApplicationsForDevice(DeviceManager.ActiveDevice.Id);
                PinnedApps = Apps.Where(a => a.DeviceInfo.Pinned).ToObservableCollection();
                OnPropertyChanged(nameof(Apps));
                OnPropertyChanged(nameof(PinnedApps));
                OnPropertyChanged(nameof(HasPinnedApps));
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading apps");
        }
    }

    private void OnApplicationListUpdated(object? sender, string deviceId)
    {   
        if (DeviceManager.ActiveDevice?.Id != deviceId) return;
        LoadApps();
    }

    private void OnApplicationItemUpdated(object? sender, (string deviceId, ApplicationInfo? appInfo, string? packageName) args)
    {
        var (deviceId, appInfo, packageName) = args;

        // Only update if this is for the active device
        if (DeviceManager.ActiveDevice?.Id != deviceId || IsLoading) return;

        App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            if (appInfo is null && packageName is not null)
            {
                // App was removed - remove it from collection
                var appToRemove = Apps.FirstOrDefault(a => a.PackageName == packageName);
                if (appToRemove is not null)
                {
                    Apps.Remove(appToRemove);
                    PinnedApps.Remove(appToRemove);
                }
            }
            else if (appInfo is not null)
            {
                var existingApp = Apps.FirstOrDefault(a => a.PackageName == appInfo.PackageName);
                
                if (existingApp is not null)
                {
                    // Update existing app
                    existingApp.PackageName = appInfo.PackageName;
                    existingApp.AppName = appInfo.AppName;
                    existingApp.IconPath = appInfo.IconPath;
                    existingApp.DeviceInfo = appInfo.DeviceInfo;
                    
                    // Update pinned apps
                    if (appInfo.DeviceInfo.Pinned && !PinnedApps.Contains(existingApp))
                    {
                        PinnedApps.Add(existingApp);
                    }
                    else if (!appInfo.DeviceInfo.Pinned && PinnedApps.Contains(existingApp))
                    {
                        PinnedApps.Remove(existingApp);
                    }
                }
                else
                {
                    // Add new app if it doesn't exist
                    Apps.Add(appInfo);
                    if (appInfo.DeviceInfo.Pinned)
                    {
                        PinnedApps.Add(appInfo);
                    }
                }
            }
            
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasPinnedApps));
        });
    }

    public async Task OpenApp(ApplicationInfo app)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            app.IsLoading = true;
            try
            {
                Logger.LogDebug("Opening app: {AppPackage}", app.AppName);
                var started = await ScreenMirrorService.StartScrcpy(DeviceManager.ActiveDevice!, $"--start-app={app.PackageName} --window-title=\"{app.AppName}\"", GetAppIconFilePath(app.PackageName));
                if (started)
                {
                    await Task.Delay(2000);
                }
            }
            finally
            {
                app.IsLoading = false;
            }
        });
    }

    #endregion

    public AppsViewModel()
    {
        LoadApps();
        
        RemoteAppsRepository.ApplicationListUpdated += OnApplicationListUpdated;
        RemoteAppsRepository.ApplicationItemUpdated += OnApplicationItemUpdated;
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(IDeviceManager.ActiveDevice))
                LoadApps();
        };
    }
}
