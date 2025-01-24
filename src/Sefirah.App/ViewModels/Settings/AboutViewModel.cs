using Sefirah.App.Data.Items;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Sefirah.App.ViewModels.Settings;

public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    private string version;

    public string AppName
    => Package.Current.DisplayName;

    public AboutViewModel()
    {
        var package = Package.Current;
        var packageVersion = package.Id.Version;
        Version = $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
    }

    [RelayCommand]
    private void CopyVersion()
    {
        var dataPackage = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy
        };
        dataPackage.SetText(Version);
        Clipboard.SetContent(dataPackage);
    }

    [RelayCommand]
    private async Task OpenGitHubRepo()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.GitHubRepoUrl)).AsTask();
    }

    [RelayCommand]
    private async Task OpenFeatureRequest()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.FeatureRequestUrl)).AsTask();
    }

    [RelayCommand]
    private async Task OpenBugReport()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.BugReportUrl)).AsTask();
    }

    [RelayCommand]
    private async Task OpenLibraryLink(string url)
    {
        await Launcher.LaunchUriAsync(new Uri(url)).AsTask();
    }

    [RelayCommand]
    private async Task OpenDonate()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.DonateUrl)).AsTask();
    }

    [RelayCommand]
    private async Task OpenLogs()
    {
        await Launcher.LaunchFolderAsync(Windows.Storage.ApplicationData.Current.LocalFolder);
    }

    [RelayCommand]
    private async Task OpenPrivacyPolicy()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.PrivacyPolicyUrl)).AsTask();
    }

    [RelayCommand]
    private async Task OpenLicense()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.LicenseUrl)).AsTask();
    }

    public ObservableCollection<OpenSourceLibraryItem> ThirdPartyLibraries { get; } =
    [
        // WinUI and Windows App SDK
        new("https://github.com/microsoft/microsoft-ui-xaml", "WinUI 3"),
        new("https://github.com/microsoft/WindowsAppSDK", "Windows App SDK"),
        
        // Community Toolkit
        new("https://github.com/CommunityToolkit/dotnet", "CommunityToolkit.Mvvm"),
        new("https://github.com/CommunityToolkit/Windows", "CommunityToolkit.WinUI"),
        
        // Cryptography & Security
        new("https://github.com/bcgit/bc-csharp", "BouncyCastle"),
        new("https://github.com/sshnet/SSH.NET", "SSH.NET"),
        
        // Data & Storage
        new("https://github.com/dotnet/efcore", "Microsoft.Data.Sqlite"),
        
        // Networking & Server
        new("https://github.com/chronoxor/NetCoreServer", "NetCoreServer"),
        new("https://github.com/meamod/MeaMod.DNS", "MeaMod.DNS"),
        new("https://github.com/PrimalZed/CloudSync", "CloudSync"),
        
        // Logging
        new("https://github.com/serilog/serilog", "Serilog"),
        
        // Windows Integration
        new("https://github.com/HavenDV/H.NotifyIcon", "H.NotifyIcon"),
        new("https://github.com/dotMorten/WinUIEx", "WinUIEx"),
        new("https://github.com/dahall/vanara", "Vanara.PInvoke"),
        
        // Microsoft Extensions
        new("https://github.com/dotnet/runtime", "Microsoft Extensions"),
    ];
}
