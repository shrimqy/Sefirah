using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;
using static Sefirah.Utils.IconUtils;

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
        if (DeviceManager.ActiveDevice is null) return;

        IsLoading = true;
        var message = new CommandMessage { CommandType = CommandType.RequestAppList };
        SessionManager.SendMessage(DeviceManager.ActiveDevice!.Session!, SocketMessageSerializer.Serialize(message));
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
            Apps.Remove(app);
            PinnedApps.Remove(app);
            await RemoteAppsRepository.RemoveDeviceFromApplication(app.PackageName, DeviceManager.ActiveDevice!.Id);
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasPinnedApps));
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
            Logger.LogInformation("Loading apps");
            IsLoading = true;

            if (DeviceManager.ActiveDevice is null) return;

            await RemoteAppsRepository.LoadApplicationsFromDevice(DeviceManager.ActiveDevice.Id);
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
            await App.MainWindow.DispatcherQueue.EnqueueAsync(() => IsLoading = false);
        }
    }

    private void OnApplicationListUpdated(object? sender, string deviceId)
    {
        App.MainWindow.DispatcherQueue.EnqueueAsync(() => IsLoading = false);
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasPinnedApps));
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
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName is nameof(IDeviceManager.ActiveDevice))
                LoadApps();
        };
    }
}
