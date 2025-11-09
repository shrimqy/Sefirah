using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Enums;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Sefirah.Services;

public class ClipboardService(
    ILogger<ClipboardService> logger,
    ISessionManager sessionManager,
    IPlatformNotificationHandler platformNotificationHandler,
    IDeviceManager deviceManager,
    IFileTransferService fileTransferService) : IClipboardService
{
    private readonly DispatcherQueue dispatcher = App.MainWindow.DispatcherQueue;
    private bool isMonitoring = false;
    private const int DirectTransferThreshold = 2 * 1024 * 1024; // 2MB threshold

    private static readonly Dictionary<string, string> SupportedImageFileTypes = new()
    {
        ["jpeg"] = "image/jpeg",
        ["jpg"] = "image/jpeg",
        ["png"] = "image/png",
        ["gif"] = "image/gif",
        ["bmp"] = "image/bmp",
        ["webp"] = "image/webp",
        ["tiff"] = "image/tiff",
        ["tif"] = "image/tiff",
        ["heic"] = "image/heic",
        ["heif"] = "image/heic",
        [".apng"] = "image/apng"
    };
    
    private bool isInternalUpdate; // To track if the clipboard change came from the remote device

    public void Initialize()
    {
        sessionManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        fileTransferService.FileReceived += OnFileReceived;
    }

    private async void OnFileReceived(object? sender, (PairedDevice device, StorageFile data) e)
    {
        await SetContentAsync(e.data, e.device);
    }

    private void OnConnectionStatusChanged(object? sender, (PairedDevice Device, bool IsConnected) e)
    {
        var devicesWithClipboardSync = deviceManager.PairedDevices
            .Where(device => device.Session is not null &&
                device.DeviceSettings.ClipboardSyncEnabled);

        dispatcher.EnqueueAsync(() =>
        {
            try
            {
                if (!devicesWithClipboardSync.Any() && isMonitoring)
                {
                    Clipboard.ContentChanged -= OnClipboardContentChanged;
                    logger.LogInformation("Clipboard monitoring stopped");
                    isMonitoring = false;
                }
                else if (!isMonitoring)
                {
                    Clipboard.ContentChanged += OnClipboardContentChanged;
                    logger.LogInformation("Clipboard monitoring started");
                    isMonitoring = true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to start clipboard monitoring {ex}", ex);
            }
        });
    }

    private async void OnClipboardContentChanged(object? sender, object? e)
    {
        if (isInternalUpdate)
            return;

        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                // Check if any connected devices have clipboard sync enabled
                var devicesWithClipboardSync = deviceManager.PairedDevices
                    .Where(device => device.Session is not null &&
                        device.DeviceSettings.ClipboardSyncEnabled)
                    .ToList();

                if (devicesWithClipboardSync.Count == 0) return;

                logger.LogDebug("Sending clipboard content");

                var dataPackageView = Clipboard.GetContent();

                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    await TryHandleTextContent(dataPackageView, devicesWithClipboardSync);
                    return;
                }

                // Check if any device has image clipboard enabled
                var devicesWithImageSync = devicesWithClipboardSync
                    .Where(d => d.DeviceSettings.ImageToClipboardEnabled)
                    .ToList();

                if (devicesWithImageSync.Count == 0) return; 

                if (dataPackageView.Contains(StandardDataFormats.StorageItems))
                {
                    var storageItems = await dataPackageView.GetStorageItemsAsync();
                    var file = storageItems.OfType<StorageFile>().FirstOrDefault();
                    if (file is IStorageFile)
                    {
                        var mimeType = file.ContentType;
                        var fileExtension = file.FileType[1..];

                        // Validate that this is a supported image type and get MIME type
                        if (!SupportedImageFileTypes.TryGetValue(fileExtension, out var detectedMimeType))
                            return;

                        // Content type from StorageFile can be unreliable
                        if (string.IsNullOrEmpty(mimeType))
                        {
                            mimeType = detectedMimeType;
                        }

                        logger.LogInformation("fileName: {fileName}, fileExtension: {fileExtension}, Mime type: {mimeType}", file.Name, fileExtension, mimeType);

                        if ((long)(await file.GetBasicPropertiesAsync()).Size > DirectTransferThreshold)
                            await HandleLargeImageTransfer(file, fileExtension, mimeType, devicesWithImageSync);
                        else
                            await HandleSmallImageTransfer(await file.OpenStreamForReadAsync(), mimeType, devicesWithImageSync);
                    }
                    return;
                }

                if (dataPackageView.Contains(StandardDataFormats.Bitmap))
                {
                    var bitmapRef = await dataPackageView.GetBitmapAsync();  
                    using var bitmap = await bitmapRef.OpenReadAsync();
#if WINDOWS
                    var stream = new MemoryStream();
                    var decoder = await BitmapDecoder.CreateAsync(bitmap);
                    var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                    encoder.SetSoftwareBitmap(softwareBitmap);
                    await encoder.FlushAsync();
                    stream.Position = 0;
#else
                    var stream = bitmap.AsStream();
#endif
                    await HandleSmallImageTransfer(stream, "image/png", devicesWithImageSync);
                }
            }
            catch (Exception ex)
            {
                logger.LogError("Error handling clipboard content: {ex}", ex);
            }
        });
    }
    

    private async Task TryHandleTextContent(DataPackageView dataPackageView, List<PairedDevice> devices)
    {
        if (!dataPackageView.Contains(StandardDataFormats.Text)) return;

        string? text = await dataPackageView.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return;

        // Convert Windows CRLF to Unix LF 
        text = text.Replace("\r\n", "\n");
        
        var message = new ClipboardMessage
        {
            Content = text,
            ClipboardType = "text/plain"
        };

        var serializedMessage = SocketMessageSerializer.Serialize(message);
        foreach (var device in devices)
        {
            sessionManager.SendMessage(device.Session!, serializedMessage);
        }
        return;
    }

    private async Task HandleLargeImageTransfer(StorageFile file, string fileType, string mimeType, List<PairedDevice> devices)
    {
        var metadata = new FileMetadata
        {
            FileName = $"sefirah_clipboard_image.{fileType}",
            MimeType = mimeType,
            FileSize = (long)(await file.GetBasicPropertiesAsync()).Size
        };

        await Task.Run(async() =>
        {
            foreach (var device in devices)
            {
                await fileTransferService.SendFile(file, metadata, device, FileTransferType.Clipboard);
            }
        });
    }

    private async Task HandleSmallImageTransfer(Stream stream, string mimeType, List<PairedDevice> devices)
    {
        stream.Position = 0;
        byte[] buffer = new byte[stream.Length];
        await stream.ReadExactlyAsync(buffer);

        var message = new ClipboardMessage
        {
            Content = Convert.ToBase64String(buffer),
            ClipboardType = mimeType
        };
        var serializedMessage = SocketMessageSerializer.Serialize(message);

        foreach (var device in devices)
        {
            sessionManager.SendMessage(device.Session!, serializedMessage);
        }
    }

    public async Task SetContentAsync(object content, PairedDevice sourceDevice)
    {
        if (!sourceDevice.DeviceSettings.ClipboardSyncEnabled) return;

        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                isInternalUpdate = true;
                var dataPackage = new DataPackage();

                switch (content)
                {
                    case StorageFile file:
                        // Set package family name for proper file handling
                        dataPackage.Properties.PackageFamilyName =
                            Package.Current.Id.FamilyName;
                        // Pass false as second parameter to indicate the app isn't taking ownership of the files
                        dataPackage.SetStorageItems([file], false);
                        break;
                    case string textContent:
                        dataPackage.SetText(textContent);
                        Uri.TryCreate(textContent, UriKind.Absolute, out Uri? uri);
                        bool isValidUri = IsValidWebUrl(uri);
                        if (sourceDevice.DeviceSettings.OpenLinksInBrowser && isValidUri)
                        {
                            await Launcher.LaunchUriAsync(uri);
                        }
                        else if (isValidUri && sourceDevice.DeviceSettings.ShowClipboardToast)
                        {
                            platformNotificationHandler.ShowClipboardNotificationWithActions(
                                "Clipboard data received",
                                "Click to open link in browser",
                                "Open in browser",
                                textContent);
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unsupported content type: {content.GetType()}");
                }

                Clipboard.SetContent(dataPackage);
                await Task.Delay(50);
                logger.LogInformation("Clipboard content set: {Content}", content);

                if (sourceDevice.DeviceSettings.ShowClipboardToast && content is not string)
                {
                    platformNotificationHandler.ShowClipboardNotification(
                        "Clipboard data received",
                        $"Content type: {content.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting clipboard content");
            }
            finally
            {
                isInternalUpdate = false;
            }
        });
    }

    public static bool IsValidWebUrl(Uri? uri)
    {
        return uri != null && 
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) && 
               !string.IsNullOrWhiteSpace(uri.Host) &&
               uri.Host.Contains('.');
    }
}
