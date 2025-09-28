using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;

namespace Sefirah.ViewModels;
public sealed partial class AppsViewModel : BaseViewModel
{
    #region Services
    private RemoteAppRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    private IAdbService AdbService { get; } = Ioc.Default.GetRequiredService<IAdbService>();
    #endregion

    #region Properties
    public ObservableCollection<ApplicationInfo> Apps => RemoteAppsRepository.Applications;
    public ObservableCollection<ApplicationInfo> PinnedApps { get; } = [];

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
        if (DeviceManager.ActiveDevice == null) return;

        IsLoading = true;
        var message = new CommandMessage { CommandType = CommandType.RequestAppList };
        SessionManager.SendMessage(DeviceManager.ActiveDevice!.Session!, SocketMessageSerializer.Serialize(message));
    }

    [RelayCommand]
    public void PinApp(ApplicationInfo app)
    {
        try
        {
            if (app == null || DeviceManager.ActiveDevice == null) return;

            if (app.DeviceInfo.Pinned)
            {
                app.DeviceInfo.Pinned = false;
                RemoteAppsRepository.UnpinApp(app, DeviceManager.ActiveDevice.Id);
                PinnedApps.Remove(app);
            }
            else
            {
                app.DeviceInfo.Pinned = true;
                RemoteAppsRepository.PinApp(app, DeviceManager.ActiveDevice.Id);
                PinnedApps.Add(app);
            }
            OnPropertyChanged(nameof(HasPinnedApps));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error pinning app: {AppPackage}", app?.PackageName);
        }
    }

    [RelayCommand]
    public async Task UninstallApp(ApplicationInfo app)
    {
        try
        {
            if (app == null) return;

            await AdbService.UninstallApp(DeviceManager.ActiveDevice!.Id, app.PackageName);
            Apps.Remove(app);
            await RemoteAppsRepository.RemoveDeviceFromApplication(app.PackageName, DeviceManager.ActiveDevice!.Id);
            OnPropertyChanged(nameof(IsEmpty));
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
            IsLoading = true;

            var activeDevice = DeviceManager.ActiveDevice;
            if (activeDevice == null) return;

            await RemoteAppsRepository.LoadApplicationsFromDevice(activeDevice.Id);
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                PinnedApps.Clear();
                foreach (var app in Apps.Where(a => a.DeviceInfo.Pinned))
                {
                    PinnedApps.Add(app);
                }
                OnPropertyChanged(nameof(HasPinnedApps));
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading apps");
        }
        finally
        {
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
            {
                IsLoading = false;
            });
        }
    }

    private void OnApplicationListUpdated(object? sender, string deviceId)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(() =>
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasPinnedApps));
        });
    }

    public async Task OpenApp(string appPackage, string appName)
    {
        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            try
            {
                var activeDevice = DeviceManager.ActiveDevice;
                if (activeDevice == null)
                {
                    Logger.LogWarning("Cannot open app - no active device");
                    return;
                }

                var app = Apps.FirstOrDefault(a => a.PackageName == appPackage);
                if (app == null)
                {
                    Logger.LogWarning("App not found: {AppPackage}", appPackage);
                    return;
                }

                var index = Apps.IndexOf(app);
                try
                {
                    Apps[index].IsLoading = true;
                    Logger.LogDebug("Opening app: {AppPackage} on device: {DeviceId}", appPackage, activeDevice.Id);

                    // Use the icon path directly for saving
                    var filePath = app.IconPath;
                    var started = await ScreenMirrorService.StartScrcpy(device: activeDevice, customArgs: $"--start-app={appPackage} --window-title=\"{appName}\"", iconPath: filePath);
                    if (started)
                    {
                        await Task.Delay(2000);
                    }
                }
                finally
                {
                    Apps[index].IsLoading = false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening app: {AppPackage}", appPackage);
            }
        });
    }

    #endregion

    public AppsViewModel()
    {
        LoadApps();
        
        RemoteAppsRepository.ApplicationListUpdated += OnApplicationListUpdated;
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IDeviceManager.ActiveDevice))
                LoadApps();
        };
    }
}
