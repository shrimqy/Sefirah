namespace Sefirah;
public static class Constants
{
    public static class Notification
    {
        public const string NotificationGroup = "file-transfer";
    }

    public static class LocalSettings
    {
        public const string DateTimeFormat = "datetimeformat";

        public const string SettingsFolderName = "settings";
        public const string UserSettingsFileName = "user_settings.json";
        public const string DatabaseFileName = "sefirah.db";
        public static readonly string ConnectionString = $"Filename={Path.Combine(ApplicationData.Current.LocalFolder.Path, DatabaseFileName)}";
    }

    public static class ExternalUrl
    {
        public const string ReleasesUrl = @"https://github.com/shrimqy/Sefirah/releases/latest";
        public const string AndroidGitHubRepoUrl = @"https://github.com/shrimqy/Sefirah-Android";
        public const string GitHubRepoUrl = @"https://github.com/shrimqy/Sefirah";
        public const string DiscordUrl = @"https://discord.gg/MuvMqv4MES";
        public const string FeatureRequestUrl = @"https://github.com/shrimqy/Sefirah/issues/new?template=request_feature.yml";
        public const string BugReportUrl = @"https://github.com/shrimqy/Sefirah/issues/new?template=report_issue.yml";
        public const string PrivacyPolicyUrl = @"https://github.com/shrimqy/Sefirah/blob/master/.github/Privacy.md";
        public const string LicenseUrl = @"https://github.com/shrimqy/Sefirah/blob/master/LICENSE";
        public const string DonateUrl = @"https://linktr.ee/shrimqy";
    }

    public static class UserEnvironmentPaths
    {
        public static readonly string DownloadsPath = GetDownloadsPath();
        public static readonly string UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string DefaultRemoteDevicePath = Path.Combine(UserProfilePath, "RemoteDevices");
        private static string GetDownloadsPath()
        {
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homePath, "Downloads");
            
        }
    }
}
