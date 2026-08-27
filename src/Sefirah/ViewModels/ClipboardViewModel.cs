using CommunityToolkit.WinUI;
using Sefirah.Data.AppDatabase.Repository;
using Sefirah.Data.Models;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Sefirah.ViewModels;

/// <summary>
/// The history of what the device has copied. Android keeps none of its own, so this is built here
/// from the items the device sends as they happen.
/// </summary>
public sealed partial class ClipboardViewModel : BaseViewModel
{
    #region Services
    private readonly IDeviceManager DeviceManager = Ioc.Default.GetRequiredService<IDeviceManager>();
    private readonly IClipboardFeature ClipboardFeature = Ioc.Default.GetRequiredService<IClipboardFeature>();
    private readonly ClipboardRepository Repository = Ioc.Default.GetRequiredService<ClipboardRepository>();
    #endregion

    private bool listening;
    private string? boundDeviceId;

    public ObservableCollection<ClipboardEntry> Entries { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    [ObservableProperty]
    public partial ClipboardEntry? SelectedEntry { get; set; }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    partial void OnStatusMessageChanged(string? value) => OnPropertyChanged(nameof(HasStatus));

    #region Lifetime

    public async Task InitializeAsync()
    {
        if (!listening)
        {
            ClipboardFeature.HistoryChanged += OnHistoryChanged;
            listening = true;
        }

        await LoadAsync();
    }

    public void Suspend()
    {
        if (!listening) return;

        ClipboardFeature.HistoryChanged -= OnHistoryChanged;
        listening = false;
    }

    private void OnHistoryChanged(object? sender, PairedDevice device)
    {
        if (boundDeviceId is not null && device.Id != boundDeviceId) return;

        dispatcher.TryEnqueue(async () => await LoadAsync());
    }

    private async Task LoadAsync()
    {
        var device = DeviceManager.ActiveDevice;
        boundDeviceId = device?.Id;

        if (device is null)
        {
            Entries.Clear();
            StatusMessage = "ClipboardNoDevice".GetLocalizedResource();
            return;
        }

        IsLoading = true;
        try
        {
            var entries = await Repository.GetEntriesAsync(device.Id);

            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }

            StatusMessage = Entries.Count == 0 ? "ClipboardEmpty".GetLocalizedResource() : null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to load clipboard history: {ex.Message}", ex);
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    /// <summary>
    /// Puts a past item back on the Windows clipboard, which is the whole point of keeping them.
    /// </summary>
    [RelayCommand]
    private async Task Copy(ClipboardEntry? entry)
    {
        if (entry is null) return;

        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };

            if (entry.IsImage)
            {
                if (!File.Exists(entry.Content))
                {
                    StatusMessage = "ClipboardImageMissing".GetLocalizedResource();
                    return;
                }

                var file = await StorageFile.GetFileFromPathAsync(entry.Content);
                package.Properties.PackageFamilyName = Package.Current.Id.FamilyName;
                package.SetBitmap(RandomAccessStreamReference.CreateFromFile(file));
                package.SetStorageItems([file], false);
            }
            else
            {
                package.SetText(entry.Content);
            }

            Clipboard.SetContent(package);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Failed to copy history entry: {ex.Message}", ex);
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task Delete(ClipboardEntry? entry)
    {
        if (entry is null) return;

        await Repository.DeleteAsync(entry.Id);
        Entries.Remove(entry);

        if (Entries.Count == 0)
        {
            StatusMessage = "ClipboardEmpty".GetLocalizedResource();
        }
    }

    [RelayCommand]
    private async Task Clear()
    {
        if (boundDeviceId is null || Entries.Count == 0) return;

        var dialog = new ContentDialog
        {
            Title = "ClipboardClearTitle".GetLocalizedResource(),
            Content = "ClipboardClearSubtitle".GetLocalizedResource(),
            PrimaryButtonText = "Remove".GetLocalizedResource(),
            CloseButtonText = "Cancel".GetLocalizedResource(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = App.MainWindow.Content!.XamlRoot
        };

        if (await dialog.ShowAsync() is not ContentDialogResult.Primary) return;

        await Repository.ClearAsync(boundDeviceId);
        Entries.Clear();
        StatusMessage = "ClipboardEmpty".GetLocalizedResource();
    }

    #endregion
}
