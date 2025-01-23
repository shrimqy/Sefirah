using System.IO;
using Windows.Storage;

namespace Sefirah.App;
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
        public const string GitHubRepoUrl = @"https://github.com/shrimqy/Sefirah";
        public const string DocumentationUrl = @"https://files.community/docs";
        public const string DiscordUrl = @"https://discord.gg/MuvMqv4MES";
        public const string FeatureRequestUrl = @"https://github.com/files-community/Files/issues/new?labels=feature+request&template=feature_request.yml";
        public const string BugReportUrl = @"https://github.com/files-community/Files/issues/new?labels=bug&template=bug_report.yml";
        public const string PrivacyPolicyUrl = @"https://files.community/privacy";
    }

    public static class UserEnvironmentPaths
    {
        public static readonly string DownloadsPath = UserDataPaths.GetDefault().Downloads;
        public static readonly string UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        public static readonly string DefaultRemoteDevicePath = Path.Combine(UserProfilePath, "RemoteDevice");
    }
}
