using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Sefirah.Utils;
using Uno.Extensions.Specialized;

namespace Sefirah.ViewModels;

public sealed partial class AppsViewModel : BaseViewModel
{
    #region Services
    private RemoteAppRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private IAdbService AdbService { get; } = Ioc.Default.GetRequiredService<IAdbService>();
    private IAppShortcutService AppShortcutService { get; } = Ioc.Default.GetRequiredService<IAppShortcutService>();
    #endregion

    #region Properties
    public ObservableCollection<ApplicationItem> Apps { get; set; } = [];
    public ObservableCollection<ApplicationItem> PinnedApps { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    public bool IsEmpty => !Apps.Any() && !IsLoading;
    public bool HasPinnedApps => PinnedApps.Any();
    public PairedDevice? ActiveDevice => DeviceManager.ActiveDevice;

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
        DeviceManager.ActiveDevice.SendMessage(new RequestApplicationList());
    }

    [RelayCommand]
    public async Task InstallApp()
    {
        if (DeviceManager.ActiveDevice is not { HasAdbConnection: true })
            return;

        var file = await PickerHelper.PickFileAsync([".apk"]);
        if (file is null)
            return;

        try
        {
            var installed = await AdbService.InstallAppAsync(DeviceManager.ActiveDevice.Id, file.Path);
            if (installed)
                RefreshApps();
        }
        catch (Exception ex)
        {
            Logger.Error("Error installing app", ex);
        }
    }

    public void TogglePin(ApplicationItem app)
    {
        try
        {
            if (app.Pinned)
            {
                app.Pinned = false;
                RemoteAppsRepository.UnpinApp(app, DeviceManager.ActiveDevice!.Id);
                PinnedApps.Remove(app);
            }
            else
            {
                app.Pinned = true;
                RemoteAppsRepository.PinApp(app, DeviceManager.ActiveDevice!.Id);
                PinnedApps.Add(app);
            }
            OnPropertyChanged(nameof(HasPinnedApps));
        }
        catch (Exception ex)
        {
            Logger.Error($"Error on pin toggle: {app?.PackageName}", ex);
        }
    }

    public async void ToggleAppShortcut(ApplicationItem app)
    {
        try
        {
            if (app.AppShortcutRegistered)
            {   
                await AppShortcutService.RemoveAppShortcutAsync(app.PackageName);
            }
            else
            {
                await AppShortcutService.CreateAppShortcutAsync(app);
            }

            app.AppShortcutRegistered = AppShortcutService.IsShortcutRegistered(app.PackageName);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error toggling app shortcut: {app?.PackageName}", ex);
        }
    }

    public async void UninstallApp(ApplicationItem app)
    {
        try
        {
            if (app.AppShortcutRegistered) 
            {
                await AppShortcutService.RemoveAppShortcutAsync(app.PackageName);
            }

            var result = await AdbService.UninstallApp(ActiveDevice!.Id, app.PackageName);
            if (!result) return;

            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                Apps.Remove(app);
                PinnedApps.Remove(app);
                OnPropertyChanged(nameof(IsEmpty));
                OnPropertyChanged(nameof(HasPinnedApps));
            });
            await RemoteAppsRepository.RemoveDeviceFromApplication(app.PackageName, ActiveDevice!.Id);
        }
        catch (Exception ex)
        {
            Logger.Error($"Error uninstalling app: {app.PackageName}", ex);
        }
    }

    #endregion

    #region Methods

    private async void LoadApps()
    {
        try
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
            {
                Apps.Clear();
                PinnedApps.Clear();

                if (ActiveDevice is null) return;

                IsLoading = true;
                Apps = RemoteAppsRepository.GetApplicationsForDevice(ActiveDevice.Id).ToObservableCollection();
                PinnedApps = Apps.Where(a => a.Pinned).ToObservableCollection();
                foreach (var app in Apps) 
                {
                    app.AppShortcutRegistered = AppShortcutService.IsShortcutRegistered(app.PackageName);
                }

                OnPropertyChanged(nameof(Apps));
                OnPropertyChanged(nameof(PinnedApps));
                OnPropertyChanged(nameof(HasPinnedApps));
                IsLoading = false;
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Error loading apps", ex);
        }
    }

    private void OnActiveDeviceChanged(object? sender, PairedDevice? _)
    {
        OnPropertyChanged(nameof(ActiveDevice));
        LoadApps();
    }

    private void OnApplicationListUpdated(object? sender, string deviceId)
    {   
        if (ActiveDevice?.Id != deviceId) return;
        LoadApps();
    }

    private void OnApplicationItemUpdated(object? sender, (string deviceId, ApplicationItem? appInfo, string? packageName) args)
    {
        var (deviceId, appInfo, packageName) = args;

        // Only update if this is for the active device
        if (ActiveDevice?.Id != deviceId || IsLoading) return;

        App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            if (appInfo is null && packageName is not null)
            {
                // App was removed - remove it from collection
                var appToRemove = Apps.FirstOrDefault(a => a.PackageName == packageName);
                if (appToRemove is not null)
                {
                    Apps.Remove(appToRemove);
                    PinnedApps.Remove(appToRemove);

                    if (appToRemove.AppShortcutRegistered)
                    {
                        _ = AppShortcutService.RemoveAppShortcutAsync(appToRemove.PackageName);
                    }
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
                    existingApp.Pinned = appInfo.Pinned;
                    existingApp.Filter = appInfo.Filter;

                    // Update pinned apps
                    if (appInfo.Pinned && !PinnedApps.Contains(existingApp))
                    {
                        PinnedApps.Add(existingApp);
                    }
                    else if (!appInfo.Pinned && PinnedApps.Contains(existingApp))
                    {
                        PinnedApps.Remove(existingApp);
                    }
                }
                else
                {
                    // Add new app if it doesn't exist
                    appInfo.AppShortcutRegistered = AppShortcutService.IsShortcutRegistered(appInfo.PackageName);
                    Apps.Add(appInfo);
                    if (appInfo.Pinned)
                    {
                        PinnedApps.Add(appInfo);
                    }
                }
            }
            
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasPinnedApps));
        });
    }

    public async Task OpenApp(ApplicationItem app)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            app.IsLoading = true;
            try
            {
                Logger.Debug($"Opening app: {app.AppName}");
                var started = await ScreenMirrorService.StartScrcpy(ActiveDevice!, app);
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
        DeviceManager.ActiveDeviceChanged += OnActiveDeviceChanged;
    }
}
