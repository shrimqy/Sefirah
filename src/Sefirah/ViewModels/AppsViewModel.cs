using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Models;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Services;
using Sefirah.Utils;
using Sefirah.Utils.Serialization;

namespace Sefirah.ViewModels;
public sealed partial class AppsViewModel : BaseViewModel
{
    public RemoteAppRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<RemoteAppRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private IDeviceManager DeviceManager { get; } = Ioc.Default.GetRequiredService<IDeviceManager>();
    private ISessionManager SessionManager { get; } = Ioc.Default.GetRequiredService<ISessionManager>();
    public ObservableCollection<ApplicationInfoEntity> Apps => RemoteAppsRepository.Applications;
    private IAdbService AdbService { get; } = Ioc.Default.GetRequiredService<IAdbService>();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? Name { get; set; }

    public bool IsEmpty => !Apps.Any() && !IsLoading;

    public AppsViewModel()
    {
        LoadApps();
        
        // Subscribe to application list updates to stop loading
        RemoteAppsRepository.ApplicationListUpdated += OnApplicationListUpdated;
        ((INotifyPropertyChanged)DeviceManager).PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(IDeviceManager.ActiveDevice))
                LoadApps();
        };
    }


    private void LoadApps()
    {
        try
        {
            IsLoading = true;

            var activeDevice = DeviceManager.ActiveDevice;
            if (activeDevice == null)
            {
                Logger.LogDebug("No active device, clearing apps");
                return;
            }
            
            RemoteAppsRepository.LoadApplicationsFromDevice(activeDevice.Id);
        
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading apps");
        }
        finally
        {
            // Add a small delay to make loading indicator visible, then clear it
            App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
            {
                await Task.Delay(200); 
                IsLoading = false;
            });
        }
    }

    private void OnApplicationListUpdated(object? sender, string deviceId)
    {
        App.MainWindow!.DispatcherQueue.EnqueueAsync(() =>
        {
            IsLoading = false;
            LoadApps();
            OnPropertyChanged(nameof(IsEmpty));
        });
    }

    [RelayCommand]
    public async Task UninstallApp(ApplicationInfoEntity app)
    {
        try
        {
            if (app == null) return;

            await AdbService.UninstallApp(DeviceManager.ActiveDevice!.Id, app.AppPackage);
            Apps.Remove(app);
            RemoteAppsRepository.RemoveDeviceFromApplication(app.AppPackage, DeviceManager.ActiveDevice!.Id);
            OnPropertyChanged(nameof(IsEmpty));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uninstalling app: {AppPackage}", app?.AppPackage);
        }
    }

    public async Task OpenApp(string appPackage, string appName)
    {
        await App.MainWindow!.DispatcherQueue.EnqueueAsync(async () =>
        {
            try
            {
                var activeDevice = DeviceManager.ActiveDevice;
                if (activeDevice == null)
                {
                    Logger.LogWarning("Cannot open app - no active device");
                    return;
                }

                var app = Apps.FirstOrDefault(a => a.AppPackage == appPackage);
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
                    var filePath = await ImageUtils.SaveToFilePathAsync(app.AppIconBytes, "appIcon.png");

                    var started = await ScreenMirrorService.StartScrcpy(device: activeDevice, customArgs: $"--start-app={appPackage} --window-title={appName}", iconPath: filePath);
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

    [RelayCommand]
    public void RefreshApps()
    {
        if (DeviceManager.ActiveDevice == null) return;
        
        IsLoading = true;
        var message = new CommandMessage { CommandType = CommandType.RequestAppList };
        SessionManager.SendMessage(DeviceManager.ActiveDevice!.Session!, SocketMessageSerializer.Serialize(message));
    }
}
