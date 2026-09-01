using Renci.SshNet;
using Sefirah.Data.Models;
using Sefirah.Utils;

namespace Sefirah.ViewModels;

/// <summary>
/// Browses the device filesystem over the SFTP endpoint the device announces while connected,
/// independently of whether the platform managed to mount it locally.
/// </summary>
public sealed partial class FilesViewModel : BaseViewModel
{
    #region Services
    private readonly IDeviceManager DeviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly ISftpFeature SftpFeature = Ioc.Default.GetRequiredService<ISftpFeature>();
    private readonly ISessionManager SessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
    #endregion

    // One navigation at a time: overlapping listings fight over the connection and over the bound collections
    private readonly SemaphoreSlim navigationLock = new(1, 1);
    private readonly DeviceSftpConnection connection = new();

    private bool listening;
    private string? boundDeviceId;
    private string rootPath = "/";
    private string currentPath = "/";

    public ObservableCollection<RemoteFileItem> Items { get; } = [];

    /// <summary>
    /// Replaced wholesale rather than mutated: BreadcrumbBar does not survive its source being
    /// cleared and refilled item by item.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> PathSegments { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> Volumes { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial int SelectedVolumeIndex { get; set; }

    public bool HasMultipleVolumes => Volumes.Count > 1;

    public bool CanGoUp => currentPath.TrimEnd('/') != rootPath.TrimEnd('/');

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnVolumesChanged(IReadOnlyList<string> value) => OnPropertyChanged(nameof(HasMultipleVolumes));

    partial void OnSelectedVolumeIndexChanged(int value)
    {
        var paths = connection.CurrentSession()?.Paths;
        if (paths is null || value < 0 || value >= paths.Count) return;

        // The ComboBox echoes the volume back while its source is being set up; only act on a real change
        if (paths[value].TrimEnd('/') == rootPath.TrimEnd('/')) return;

        rootPath = paths[value];
        _ = NavigateAsync(rootPath);
    }

    #region Lifetime

    /// <summary>
    /// Picks up the endpoint of the currently active device and lists its root.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (!listening)
        {
            SftpFeature.SessionChanged += OnSessionChanged;
            SessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            listening = true;
        }

        await LoadRootAsync();
    }

    /// <summary>
    /// Stops following the device and drops the connection, so it is not kept busy while the page is away.
    /// </summary>
    public void Suspend()
    {
        if (listening)
        {
            SftpFeature.SessionChanged -= OnSessionChanged;
            SessionManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
            listening = false;
        }

        connection.Drop();
    }

    /// <summary>
    /// The device announced a new endpoint, which means new credentials: rebuild and stay where the user was.
    /// </summary>
    private void OnSessionChanged(object? sender, PairedDevice device)
    {
        if (boundDeviceId is not null && device.Id != boundDeviceId) return;

        connection.Drop();
        dispatcher.TryEnqueue(async () =>
        {
            var current = connection.CurrentSession();
            if (current is null) return;

            var paths = current.Paths.Count > 0 ? current.Paths : ["/"];
            rootPath = paths[Math.Clamp(SelectedVolumeIndex, 0, paths.Count - 1)];

            // Come back to the folder the user was looking at, falling back to the root if it is gone
            var target = currentPath.StartsWith(rootPath, StringComparison.Ordinal) ? currentPath : rootPath;
            await NavigateAsync(target);
        });
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (boundDeviceId is not null && device.Id != boundDeviceId) return;
        if (device.IsConnected) return;

        connection.Drop();
        dispatcher.TryEnqueue(() =>
        {
            Items.Clear();
            StatusMessage = "FilesDisconnected".GetLocalizedResource();
        });
    }

    private async Task LoadRootAsync()
    {
        boundDeviceId = DeviceManager.ActiveDevice?.Id;
        var current = connection.CurrentSession();
        Items.Clear();

        if (current is null)
        {
            Volumes = [];
            PathSegments = [];
            StatusMessage = "FilesUnavailable".GetLocalizedResource();
            return;
        }

        var paths = current.Paths.Count > 0 ? current.Paths : ["/"];

        // Set the root before the index, so the change handler recognises the echo and stays quiet
        rootPath = paths[0];
        Volumes = [.. paths.Select((path, i) => current.PathNames.Count > i ? current.PathNames[i] : path)];
        SelectedVolumeIndex = 0;

        await NavigateAsync(rootPath);
    }

    #endregion

    #region Navigation

    [RelayCommand]
    private Task Refresh() => NavigateAsync(currentPath);

    [RelayCommand]
    private Task GoUp()
    {
        if (!CanGoUp) return Task.CompletedTask;

        var parent = currentPath.TrimEnd('/');
        var separator = parent.LastIndexOf('/');
        parent = separator <= 0 ? "/" : parent[..separator];
        return NavigateAsync(parent);
    }

    /// <summary>
    /// Navigates to the breadcrumb segment at <paramref name="index"/>, 0 being the volume root.
    /// </summary>
    public Task NavigateToSegmentAsync(int index)
    {
        if (index < 0 || index >= PathSegments.Count) return Task.CompletedTask;

        var path = rootPath.TrimEnd('/');
        for (var i = 1; i <= index; i++)
        {
            path += "/" + PathSegments[i];
        }
        return NavigateAsync(string.IsNullOrEmpty(path) ? "/" : path);
    }

    [RelayCommand]
    private async Task Open(RemoteFileItem? item)
    {
        if (item is null) return;

        if (item.IsDirectory)
        {
            await NavigateAsync(item.FullPath);
            return;
        }

        await RunAsync(async () =>
        {
            // Files are streamed to a temp copy first, there is nothing to open in place over SFTP
            var local = Path.Combine(Path.GetTempPath(), "Sefirah", item.Name);
            Directory.CreateDirectory(Path.GetDirectoryName(local)!);

            await connection.RunAsync(sftp =>
            {
                using var stream = File.Create(local);
                sftp.DownloadFile(item.FullPath, stream);
                return true;
            });

            Process.Start(new ProcessStartInfo(local) { UseShellExecute = true });
        });
    }

    private async Task NavigateAsync(string path)
    {
        await navigationLock.WaitAsync();
        try
        {
            IsLoading = true;
            StatusMessage = null;

            var entries = await connection.RunAsync(sftp => sftp
                .ListDirectory(path)
                .Where(entry => entry.Name is not "." and not "..")
                .Select(entry => new RemoteFileItem
                {
                    Name = entry.Name,
                    FullPath = entry.FullName,
                    IsDirectory = entry.IsDirectory,
                    Size = entry.IsDirectory ? 0 : entry.Length,
                    LastModified = entry.LastWriteTime
                })
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());

            currentPath = path;
            Items.Clear();
            foreach (var entry in entries)
            {
                Items.Add(entry);
            }

            PathSegments = BuildBreadcrumb();
            if (Items.Count == 0)
            {
                StatusMessage = "FilesEmptyFolder".GetLocalizedResource();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to list {path}: {ex.Message}", ex);
            StatusMessage = Describe(ex);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanGoUp));
            navigationLock.Release();
        }
    }

    private IReadOnlyList<string> BuildBreadcrumb()
    {
        List<string> segments = [Volumes.Count > 0 ? Volumes[Math.Clamp(SelectedVolumeIndex, 0, Volumes.Count - 1)] : "/"];

        var relative = currentPath.Length > rootPath.Length ? currentPath[rootPath.Length..] : string.Empty;
        segments.AddRange(relative.Split('/', StringSplitOptions.RemoveEmptyEntries));
        return segments;
    }

    #endregion

    #region File operations

    [RelayCommand]
    private async Task Download(RemoteFileItem? item)
    {
        if (item is null || item.IsDirectory) return;

        var folder = await PickerHelper.PickFolderAsync();
        if (folder is null) return;

        await RunAsync(async () =>
        {
            var local = Path.Combine(folder.Path, item.Name);
            await connection.RunAsync(sftp =>
            {
                using var stream = File.Create(local);
                sftp.DownloadFile(item.FullPath, stream);
                return true;
            });
        });
    }

    [RelayCommand]
    private async Task Upload()
    {
        var file = await PickerHelper.PickFileAsync();
        if (file is null) return;

        await RunAsync(async () =>
        {
            var target = CombinePath(currentPath, file.Name);
            await connection.RunAsync(sftp =>
            {
                using var stream = File.OpenRead(file.Path);
                sftp.UploadFile(stream, target);
                return true;
            });
            await NavigateAsync(currentPath);
        });
    }

    [RelayCommand]
    private async Task NewFolder()
    {
        var name = await PromptForNameAsync(
            "FilesNewFolder".GetLocalizedResource(),
            "FilesCreate".GetLocalizedResource(),
            string.Empty);
        if (name is null) return;

        await RunAsync(async () =>
        {
            var target = CombinePath(currentPath, name);
            await connection.RunAsync(sftp => { sftp.CreateDirectory(target); return true; });
            await NavigateAsync(currentPath);
        });
    }

    [RelayCommand]
    private async Task Rename(RemoteFileItem? item)
    {
        if (item is null) return;

        var name = await PromptForNameAsync(
            "FilesRename".GetLocalizedResource(),
            "FilesRename".GetLocalizedResource(),
            item.Name);
        if (name is null || name == item.Name) return;

        await RunAsync(async () =>
        {
            var target = CombinePath(currentPath, name);
            await connection.RunAsync(sftp => { sftp.RenameFile(item.FullPath, target); return true; });
            await NavigateAsync(currentPath);
        });
    }

    [RelayCommand]
    private async Task Delete(RemoteFileItem? item)
    {
        if (item is null) return;

        var dialog = new ContentDialog
        {
            Title = "FilesDeleteTitle".GetLocalizedResource(),
            Content = string.Format("FilesDeleteSubtitle".GetLocalizedResource(), item.Name),
            PrimaryButtonText = "Remove".GetLocalizedResource(),
            CloseButtonText = "Cancel".GetLocalizedResource(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary) return;

        await RunAsync(async () =>
        {
            await connection.RunAsync(sftp =>
            {
                if (item.IsDirectory)
                {
                    DeleteRecursive(sftp, item.FullPath);
                }
                else
                {
                    sftp.DeleteFile(item.FullPath);
                }
                return true;
            });
            await NavigateAsync(currentPath);
        });
    }

    private static void DeleteRecursive(SftpClient sftp, string path)
    {
        foreach (var entry in sftp.ListDirectory(path))
        {
            if (entry.Name is "." or "..") continue;

            if (entry.IsDirectory)
            {
                DeleteRecursive(sftp, entry.FullName);
            }
            else
            {
                sftp.DeleteFile(entry.FullName);
            }
        }
        sftp.DeleteDirectory(path);
    }

    #endregion

    #region Plumbing

    private static string Describe(Exception ex) => ex switch
    {
        InvalidOperationException => "FilesUnavailable".GetLocalizedResource(),
        _ when DeviceSftpConnection.IsUnreachable(ex) => "FilesDisconnected".GetLocalizedResource(),
        _ => ex.Message
    };

    private static string CombinePath(string directory, string name)
        => directory.TrimEnd('/') + "/" + name;

    /// <summary>
    /// Runs an operation with the busy indicator on, turning any failure into a message instead of a crash.
    /// </summary>
    private async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            Logger.Warn($"File operation failed: {ex.Message}", ex);
            StatusMessage = Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static async Task<string?> PromptForNameAsync(string title, string primaryText, string initial)
    {
        var input = new TextBox
        {
            Text = initial,
            SelectionStart = 0,
            SelectionLength = initial.Length
        };

        var dialog = new ContentDialog
        {
            Title = title,
            Content = input,
            PrimaryButtonText = primaryText,
            CloseButtonText = "Cancel".GetLocalizedResource(),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        return await dialog.ShowAsync() is ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text)
            ? input.Text.Trim()
            : null;
    }

    #endregion
}
