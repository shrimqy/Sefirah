using Sefirah.App.Data.Contracts;
using Windows.Services.Store;
using WinRT.Interop;
using System.Text.Json;
using System.Net.Http;

namespace Sefirah.App.Services;
internal sealed partial class AppUpdateService : ObservableObject, IUpdateService
{
    private readonly ILogger logger;

    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        IsUpdateAvailable = false;
        logger.Info("Checking for updates...");

        IsUpdateAvailable = await GetUpdatePackagesAsync();

        return IsUpdateAvailable;
    }

    public AppUpdateService(ILogger logger)
    {
        this.logger = logger;
    }


    private async Task<bool> GetUpdatePackagesAsync()
    {
        try
        {
            // Get current app version
            Version currentVersion = GetCurrentAppVersion();

            // Get latest release version from GitHub
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("Sefirah", currentVersion.ToString()));
            
            string releaseUrl = $"https://api.github.com/repos/shrimqy/Sefirah/releases/latest";
            
            HttpResponseMessage response = await client.GetAsync(releaseUrl);
            response.EnsureSuccessStatusCode();
            
            string jsonContent = await response.Content.ReadAsStringAsync();
            JsonDocument document = JsonDocument.Parse(jsonContent);
            string tagName = document?.RootElement.GetProperty("tag_name").GetString();

            if (string.IsNullOrEmpty(tagName)) return false;
            string versionString = tagName[1..];

            if (Version.TryParse(versionString, out Version? latestVersion))
            {
                return latestVersion > currentVersion;;
            }
            
            logger.Warn($"Failed to parse GitHub release version: {versionString}");
            return false;
        }
        catch (Exception ex)
        {
            logger.Warn($"Error checking for updates: {ex.Message}", ex);
            return false;
        }
    }

    private Version GetCurrentAppVersion()
    {
        var package = Windows.ApplicationModel.Package.Current;
        var packageVersion = package.Id.Version;
        
        return new Version(
            packageVersion.Major,
            packageVersion.Minor,
            packageVersion.Build
        );
    }

}
