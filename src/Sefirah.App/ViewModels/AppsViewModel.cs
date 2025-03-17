using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;

namespace Sefirah.App.ViewModels;
public sealed class AppsViewModel : BaseViewModel
{
    public IRemoteAppsRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<IRemoteAppsRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    
    private ObservableCollection<ApplicationInfoEntity> _apps = [];
    public ObservableCollection<ApplicationInfoEntity> Apps 
    { 
        get => _apps;
        set => SetProperty(ref _apps, value);
    }
    
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
        var apps = await RemoteAppsRepository.GetInstalledAppsAsync();
        
        dispatcher.TryEnqueue(() =>
        {
            Apps = [.. apps];
            IsLoading = false;
            OnPropertyChanged(nameof(IsEmpty));
        });
    }

    public async Task OpenApp(string appPackage)
    {
        await ScreenMirrorService.StartScrcpy(customArgs: $"--new-display --start-app={appPackage}");
    }
}
