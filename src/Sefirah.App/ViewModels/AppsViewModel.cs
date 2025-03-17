using CommunityToolkit.WinUI;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;

namespace Sefirah.App.ViewModels;
public sealed class AppsViewModel : BaseViewModel
{
    public IRemoteAppsRepository RemoteAppsRepository { get; } = Ioc.Default.GetRequiredService<IRemoteAppsRepository>();
    private IScreenMirrorService ScreenMirrorService { get; } = Ioc.Default.GetRequiredService<IScreenMirrorService>();
    
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
        await ScreenMirrorService.StartScrcpy(customArgs: $"--new-display --start-app={appPackage}");
    }
}
