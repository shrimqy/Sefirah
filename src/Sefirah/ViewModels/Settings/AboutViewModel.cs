using Sefirah.Data.Items;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Sefirah.ViewModels.Settings;
public partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Version { get; set; } = string.Empty;

    public string AppName => Package.Current.DisplayName;

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
    private Task OpenGitHubRepo()
    {
        return Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.GitHubRepoUrl)).AsTask();
    }

    [RelayCommand]
    private Task OpenAndroidGitHubRepo()
    {
        return Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.AndroidGitHubRepoUrl)).AsTask();
    }

    [RelayCommand]
    private Task OpenFeatureRequest()
    {
        return Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.FeatureRequestUrl)).AsTask();
    }

    [RelayCommand]
    private async Task OpenBugReport()
    {
        await Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.BugReportUrl)).AsTask();
    }

    [RelayCommand]
    private Task OpenLibraryLink(string url)
    {
        return Launcher.LaunchUriAsync(new Uri(url)).AsTask();
    }

    [RelayCommand]
    private Task OpenDonate()
    {
        return Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.DonateUrl)).AsTask();
    }

    [RelayCommand]
    private async Task<bool> OpenLogs()
    {
        var path = ApplicationData.Current.LocalFolder.Path;
        Debug.WriteLine(path);
        var result = await Launcher.LaunchUriAsync(new Uri(path)).AsTask();
        return result;
    }

    [RelayCommand]
    private Task OpenPrivacyPolicy()
    {
        return Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.PrivacyPolicyUrl)).AsTask();
    }

    [RelayCommand]
    private Task OpenLicense()
    {
        return Launcher.LaunchUriAsync(new Uri(Constants.ExternalUrl.LicenseUrl)).AsTask();
    }

    public ObservableCollection<OpenSourceLibraryItem> ThirdPartyLibraries { get; } =
    [
        // WinUI and Windows App SDK
        new("https://github.com/microsoft/microsoft-ui-xaml", "WinUI 3"),
        new("https://github.com/microsoft/WindowsAppSDK", "Windows App SDK"),

        // Uno Platform
        new("https://github.com/unoplatform/uno", "Uno Platform"),

        // Community Toolkit
        new("https://github.com/CommunityToolkit/dotnet", "CommunityToolkit.Mvvm"),
        new("https://github.com/CommunityToolkit/Windows", "CommunityToolkit.WinUI"),
        
        // Cryptography & Security
        new("https://github.com/bcgit/bc-csharp", "BouncyCastle"),
        new("https://github.com/sshnet/SSH.NET", "SSH.NET"),
        
        // Data & Storage
        new("https://github.com/ericsink/SQLitePCL.raw", "SQLitePCLRaw"),

        // Networking & Server
        new("https://github.com/chronoxor/NetCoreServer", "NetCoreServer"),
        new("https://github.com/meamod/MeaMod.DNS", "MeaMod.DNS"),
        new("https://github.com/PrimalZed/CloudSync", "CloudSync"),
        
        // ADB
        new("https://github.com/SharpAdb/AdvancedSharpAdbClient", "AdvancedSharpAdbClient"),
        
        // Windows Integration
        new("https://github.com/HavenDV/H.NotifyIcon", "H.NotifyIcon"),
        new("https://github.com/dotMorten/WinUIEx", "WinUIEx"),
        new("https://github.com/dahall/vanara", "Vanara.PInvoke"),
        
        // Microsoft Extensions
        new("https://github.com/dotnet/runtime", "Microsoft Extensions"),

        // Other
        new("https://github.com/tmds/Tmds.DBus", "Tmds.DBus"),
        new("https://github.com/SharpAdb/AdvancedSharpAdbClient", "AdvancedSharpAdbClient"),

    ];
}

