using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media.Imaging;
using Renci.SshNet;
using Sefirah.Data.Models;
using Sefirah.Utils;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Sefirah.ViewModels;

/// <summary>
/// A gallery of the images on the device, with the actions that make a phone gallery useful from a
/// desktop: copy one into the clipboard, keep it, or throw it away.
/// </summary>
public sealed partial class PhotosViewModel : BaseViewModel
{
    #region Services
    private readonly IDeviceManager DeviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly ISftpFeature SftpFeature = Ioc.Default.GetRequiredService<ISftpFeature>();
    private readonly ISessionManager SessionManager = Ioc.Default.GetRequiredService<ISessionManager>();
    #endregion

    /// <summary>Where phones keep pictures. Anything else is a file, not a photo.</summary>
    private static readonly string[] PhotoFolders = ["DCIM", "Pictures", "Download"];

    private static readonly string[] PhotoExtensions =
        [".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".heic"];

    private const int MaxPhotos = 60;
    private const int MaxDepth = 2;
    private const int HeadBytes = 256 * 1024;

    /// <summary>Above this, a preview is not worth pulling the whole image across the network.</summary>
    private const long PreviewSizeLimit = 8L * 1024 * 1024;

    private readonly DeviceSftpConnection connection = new();
    private readonly SemaphoreSlim loadLock = new(1, 1);

    private CancellationTokenSource? previewCts;
    private bool listening;
    private string? boundDeviceId;

    public ObservableCollection<RemotePhotoItem> Photos { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial RemotePhotoItem? SelectedPhoto { get; set; }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    #region Lifetime

    public async Task InitializeAsync()
    {
        if (!listening)
        {
            SftpFeature.SessionChanged += OnSessionChanged;
            SessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
            listening = true;
        }

        await LoadAsync();
    }

    public void Suspend()
    {
        if (listening)
        {
            SftpFeature.SessionChanged -= OnSessionChanged;
            SessionManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
            listening = false;
        }

        previewCts?.Cancel();
        connection.Drop();
    }

    private void OnSessionChanged(object? sender, PairedDevice device)
    {
        if (boundDeviceId is not null && device.Id != boundDeviceId) return;

        connection.Drop();
        dispatcher.TryEnqueue(async () => await LoadAsync());
    }

    private void OnConnectionStatusChanged(object? sender, PairedDevice device)
    {
        if (boundDeviceId is not null && device.Id != boundDeviceId) return;
        if (device.IsConnected) return;

        previewCts?.Cancel();
        connection.Drop();
        dispatcher.TryEnqueue(() =>
        {
            Photos.Clear();
            StatusMessage = "PhotosDisconnected".GetLocalizedResource();
        });
    }

    #endregion

    #region Loading

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    private async Task LoadAsync()
    {
        if (!await loadLock.WaitAsync(0)) return;

        previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        previewCts = cts;

        try
        {
            boundDeviceId = DeviceManager.ActiveDevice?.Id;
            IsLoading = true;
            StatusMessage = null;

            var session = connection.CurrentSession();
            if (session is null)
            {
                Photos.Clear();
                StatusMessage = "PhotosUnavailable".GetLocalizedResource();
                return;
            }

            var root = (session.Paths.Count > 0 ? session.Paths[0] : "/").TrimEnd('/');
            var found = await connection.RunAsync(sftp =>
            {
                List<RemotePhotoItem> photos = [];
                foreach (var folder in PhotoFolders)
                {
                    Collect(sftp, $"{root}/{folder}", 0, photos);
                }
                return photos
                    .OrderByDescending(photo => photo.LastModified)
                    .Take(MaxPhotos)
                    .ToList();
            });

            Photos.Clear();
            foreach (var photo in found)
            {
                Photos.Add(photo);
            }

            if (Photos.Count == 0)
            {
                StatusMessage = "PhotosEmpty".GetLocalizedResource();
                return;
            }

            // Previews trickle in afterwards so the grid appears immediately
            _ = LoadPreviewsAsync([.. found], cts.Token);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to list photos: {ex.Message}", ex);
            StatusMessage = Describe(ex);
        }
        finally
        {
            IsLoading = false;
            loadLock.Release();
        }
    }

    private static void Collect(SftpClient sftp, string directory, int depth, List<RemotePhotoItem> into)
    {
        if (depth > MaxDepth) return;

        try
        {
            foreach (var entry in sftp.ListDirectory(directory))
            {
                // Leading dots are thumbnails caches and trash, not the user's pictures
                if (entry.Name is "." or ".." || entry.Name.StartsWith('.')) continue;

                if (entry.IsDirectory)
                {
                    Collect(sftp, entry.FullName, depth + 1, into);
                }
                else if (PhotoExtensions.Contains(Path.GetExtension(entry.Name).ToLowerInvariant()))
                {
                    into.Add(new RemotePhotoItem
                    {
                        Name = entry.Name,
                        FullPath = entry.FullName,
                        Size = entry.Length,
                        LastModified = entry.LastWriteTime
                    });
                }
            }
        }
        catch
        {
            // A folder we cannot read is not worth failing the whole gallery over
        }
    }

    private async Task LoadPreviewsAsync(IReadOnlyList<RemotePhotoItem> photos, CancellationToken token)
    {
        foreach (var photo in photos)
        {
            if (token.IsCancellationRequested) return;

            try
            {
                var bytes = await FetchPreviewBytesAsync(photo, token);
                if (bytes is null || token.IsCancellationRequested) continue;

                await dispatcher.EnqueueAsync(async () =>
                {
                    var bitmap = new BitmapImage { DecodePixelWidth = 320 };
                    using var stream = new InMemoryRandomAccessStream();
                    await stream.WriteAsync(bytes.AsBuffer());
                    stream.Seek(0);
                    await bitmap.SetSourceAsync(stream);
                    photo.Preview = bitmap;
                });
            }
            catch (Exception ex)
            {
                Logger.Warn($"No preview for {photo.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Reads the head of the image first: that is often the whole file, and for camera shots it
    /// carries an embedded preview, which saves pulling megabytes for a thumbnail.
    /// </summary>
    private async Task<byte[]?> FetchPreviewBytesAsync(RemotePhotoItem photo, CancellationToken token)
    {
        var cached = CachePathFor(photo);

        // Only trust a cached copy that is the whole picture; a partial one would render as a torn image
        if (File.Exists(cached) && new FileInfo(cached).Length == photo.Size)
        {
            photo.LocalPath = cached;
            return await File.ReadAllBytesAsync(cached, token);
        }

        return await connection.RunAsync<byte[]?>(sftp =>
        {
            // Small enough that reading the head means reading the whole picture
            if (photo.Size <= HeadBytes)
            {
                var whole = new byte[photo.Size];
                using (var stream = sftp.OpenRead(photo.FullPath))
                {
                    stream.ReadExactly(whole);
                }

                Cache(cached, whole);
                photo.LocalPath = cached;
                return whole;
            }

            var head = new byte[HeadBytes];
            using (var stream = sftp.OpenRead(photo.FullPath))
            {
                // ReadExactly, because a single Read over the network returns whatever has arrived so far
                stream.ReadExactly(head);
            }

            if (ExifThumbnail.TryExtract(head) is { } embedded) return embedded;

            if (photo.Size > PreviewSizeLimit) return null;

            Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
            using (var file = File.Create(cached))
            {
                sftp.DownloadFile(photo.FullPath, file);
            }

            photo.LocalPath = cached;
            return File.ReadAllBytes(cached);
        });
    }

    private static void Cache(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    #endregion

    #region Actions

    [RelayCommand]
    private async Task Open(RemotePhotoItem? photo)
    {
        if (photo is null) return;

        await RunAsync(async () =>
        {
            var local = await EnsureLocalCopyAsync(photo);
            Process.Start(new ProcessStartInfo(local) { UseShellExecute = true });
        });
    }

    [RelayCommand]
    private async Task Copy(RemotePhotoItem? photo)
    {
        if (photo is null) return;

        await RunAsync(async () =>
        {
            var local = await EnsureLocalCopyAsync(photo);
            var file = await StorageFile.GetFileFromPathAsync(local);

            // Both forms: as a bitmap for editors, as a file for Explorer and chat apps
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.Properties.PackageFamilyName = Package.Current.Id.FamilyName;
            package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
            package.SetStorageItems([file], false);
            Clipboard.SetContent(package);

            StatusMessage = null;
        });
    }

    [RelayCommand]
    private async Task Save(RemotePhotoItem? photo)
    {
        if (photo is null) return;

        var folder = await PickerHelper.PickFolderAsync();
        if (folder is null) return;

        await RunAsync(async () =>
        {
            var local = await EnsureLocalCopyAsync(photo);
            File.Copy(local, Path.Combine(folder.Path, photo.Name), overwrite: true);
        });
    }

    [RelayCommand]
    private async Task Delete(RemotePhotoItem? photo)
    {
        if (photo is null) return;

        var dialog = new ContentDialog
        {
            Title = "PhotosDeleteTitle".GetLocalizedResource(),
            Content = string.Format("PhotosDeleteSubtitle".GetLocalizedResource(), photo.Name),
            PrimaryButtonText = "Remove".GetLocalizedResource(),
            CloseButtonText = "Cancel".GetLocalizedResource(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary) return;

        await RunAsync(async () =>
        {
            await connection.RunAsync(sftp => { sftp.DeleteFile(photo.FullPath); return true; });
            Photos.Remove(photo);

            if (Photos.Count == 0)
            {
                StatusMessage = "PhotosEmpty".GetLocalizedResource();
            }
        });
    }

    private async Task<string> EnsureLocalCopyAsync(RemotePhotoItem photo)
    {
        var cached = CachePathFor(photo);
        if (photo.LocalPath is { } existing && File.Exists(existing) && new FileInfo(existing).Length == photo.Size)
        {
            return existing;
        }

        await connection.RunAsync(sftp =>
        {
            Directory.CreateDirectory(Path.GetDirectoryName(cached)!);
            using var file = File.Create(cached);
            sftp.DownloadFile(photo.FullPath, file);
            return true;
        });

        photo.LocalPath = cached;
        return cached;
    }

    /// <summary>
    /// Keyed on the full remote path so two pictures with the same name never collide.
    /// </summary>
    private static string CachePathFor(RemotePhotoItem photo)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(photo.FullPath)))[..12];
        return Path.Combine(Path.GetTempPath(), "Sefirah", "photos", $"{digest}_{photo.Name}");
    }

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
            Logger.Warn($"Photo action failed: {ex.Message}", ex);
            StatusMessage = Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Describe(Exception ex) => ex switch
    {
        InvalidOperationException => "PhotosUnavailable".GetLocalizedResource(),
        _ when DeviceSftpConnection.IsUnreachable(ex) => "PhotosDisconnected".GetLocalizedResource(),
        _ => ex.Message
    };

    #endregion
}
