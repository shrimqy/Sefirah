using Microsoft.UI.Dispatching;
using Sefirah.App.Data.AppDatabase.Models;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Enums;

namespace Sefirah.App.ViewModels.Settings;

public sealed partial class FeaturesViewModel : ObservableObject
{
    private readonly IUserSettingsService UserSettingsService = Ioc.Default.GetRequiredService<IUserSettingsService>();
    private readonly IRemoteAppsRepository RemoteAppsRepository = Ioc.Default.GetRequiredService<IRemoteAppsRepository>();
    private readonly DispatcherQueue dispatcherQueue;

    public ObservableCollection<ApplicationInfoEntity> NotificationPreferences { get; } = [];

    public bool IsClipboardExpanded { get; set; }
    public bool IsNotificationExpanded { get; set; }
    public bool IsAppNotificationExpanded { get; set; }

    // Clipboard Settings
    public bool ClipboardSyncEnabled
    {
        get => UserSettingsService.FeatureSettingsService.ClipboardSyncEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ClipboardSyncEnabled)
            {
                UserSettingsService.FeatureSettingsService.ClipboardSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ImageToClipboardEnabled
    {
        get => UserSettingsService.FeatureSettingsService.ImageToClipboardEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ImageToClipboardEnabled)
            {
                UserSettingsService.FeatureSettingsService.ImageToClipboardEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowClipboardToast
    {
        get => UserSettingsService.FeatureSettingsService.ShowClipboardToast;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ShowClipboardToast)
            {
                UserSettingsService.FeatureSettingsService.ShowClipboardToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool OpenLinksInBrowser
    {
        get => UserSettingsService.FeatureSettingsService.OpenLinksInBrowser;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.OpenLinksInBrowser)
            {
                UserSettingsService.FeatureSettingsService.OpenLinksInBrowser = value;
                OnPropertyChanged();
            }
        }
    }

    // Notification Settings
    public bool NotificationSyncEnabled
    {
        get => UserSettingsService.FeatureSettingsService.NotificationSyncEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.NotificationSyncEnabled)
            {
                UserSettingsService.FeatureSettingsService.NotificationSyncEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowNotificationToast
    {
        get => UserSettingsService.FeatureSettingsService.ShowNotificationToast;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ShowNotificationToast)
            {
                UserSettingsService.FeatureSettingsService.ShowNotificationToast = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreNotificationDuringDnd
    {
        get => UserSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd;
        set 
        {
            if (value != UserSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd)
            {
                UserSettingsService.FeatureSettingsService.IgnoreNotificationDuringDnd = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ShowBadge
    {
        get => UserSettingsService.FeatureSettingsService.ShowBadge;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ShowBadge)
            {
                UserSettingsService.FeatureSettingsService.ShowBadge = value;
                OnPropertyChanged();
            }
        }
    }


    public string RemoteStoragePath
    {
        get => UserSettingsService.FeatureSettingsService.RemoteStoragePath;
        set
        {
            // TODO : Delete the previous remote storage folder or move all the placeholders to the new location
            if (value != UserSettingsService.FeatureSettingsService.RemoteStoragePath)
            {
                UserSettingsService.FeatureSettingsService.RemoteStoragePath = value;
                OnPropertyChanged();
            }
        }
    }

    public string ReceivedFilesPath
    {
        get => UserSettingsService.FeatureSettingsService.ReceivedFilesPath;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ReceivedFilesPath)
            {
                UserSettingsService.FeatureSettingsService.ReceivedFilesPath = value;
                OnPropertyChanged();
            }
        }
    }

    public bool ClipboardFilesEnabled
    {
        get => UserSettingsService.FeatureSettingsService.ClipboardFilesEnabled;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.ClipboardFilesEnabled)
            {
                UserSettingsService.FeatureSettingsService.ClipboardFilesEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IgnoreWindowsApps
    {
        get => UserSettingsService.FeatureSettingsService.IgnoreWindowsApps;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.IgnoreWindowsApps)
            {
                UserSettingsService.FeatureSettingsService.IgnoreWindowsApps = value;
                OnPropertyChanged();
            }
        }
    }

    public NotificationLaunchPreference NotificationLaunchPreference
    {
        get => UserSettingsService.FeatureSettingsService.NotificationLaunchPreference;
        set
        {
            if (value != UserSettingsService.FeatureSettingsService.NotificationLaunchPreference)
            {
                UserSettingsService.FeatureSettingsService.NotificationLaunchPreference = value;
                OnPropertyChanged();
            }
        }
    }

    public FeaturesViewModel()
    {
        dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        LoadNotificationPreferencesAsync();
    }

    private async void LoadNotificationPreferencesAsync()
    {
        var preferences = await RemoteAppsRepository.GetAllAsync();
        dispatcherQueue.TryEnqueue(() =>
        {
            NotificationPreferences.Clear();
            foreach (var preference in preferences)
            {
                NotificationPreferences.Add(preference);
            }
        });
    }

    public async void ChangeNotificationFilter(ApplicationInfoEntity preferences)
    {
        await RemoteAppsRepository.UpdateFilterAsync(preferences.AppPackage, preferences.NotificationFilter);
        var existingItem = NotificationPreferences.FirstOrDefault(p => p.AppPackage == preferences.AppPackage);
        
        if (existingItem != null)
        {
            dispatcherQueue.TryEnqueue(() =>
            {
                existingItem.NotificationFilter = preferences.NotificationFilter;
            });
        }
    }
}
