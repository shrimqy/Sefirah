using CommunityToolkit.WinUI;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;

namespace Sefirah.App.ViewModels;
public sealed class AppsViewModel : BaseViewModel
{
    public IRemoteAppsRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<IRemoteAppsRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    private IUserSettingsService UserSettingsService { get; } = Ioc.Default.GetRequiredService<IUserSettingsService>();

    public ObservableCollection<ApplicationInfoEntity> Apps => RemoteAppsRepository.Applications;
    
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }
    
    public bool IsEmpty => Apps.Count == 0 && !IsLoading;
    
    public AppsViewModel()
    {
        LoadApps();
    }

    private async void LoadApps()
    {
        IsLoading = true;
        await dispatcher.EnqueueAsync(async() =>
        {
            await RemoteAppsRepository.LoadApplicationsAsync();
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        });
    }

    public async Task OpenApp(string appPackage)
    {
        await dispatcher.EnqueueAsync(async() =>
        {
            var app = Apps.FirstOrDefault(a => a.AppPackage == appPackage);
            if (app == null) return;

            var index = Apps.IndexOf(app);
            try
            {
                Apps[index].IsLoading = true;
                var started = await ScreenMirrorService.StartScrcpy(customArgs: $"--start-app={appPackage}");
                if (started)
                {
                    await Task.Delay(2000);
                }
            }
            finally
            {
                Apps[index].IsLoading = false;
            }
        });
    }
}
