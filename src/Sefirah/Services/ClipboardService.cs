using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Utils.Serialization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Sefirah.Services;

public class ClipboardService : IClipboardService
{
    private readonly ILogger<ClipboardService> logger;
    private readonly ISessionManager sessionManager;
    private readonly IUserSettingsService userSettingsService;
    private readonly IPlatformNotificationHandler platformNotificationHandler;
    private readonly IDeviceManager deviceManager;
    private readonly DispatcherQueue dispatcher;
    private readonly IFileTransferService fileTransferService;
    private const int DirectTransferThreshold = 512 * 512; // 1MB threshold
    
    private bool isInternalUpdate; // To track if the clipboard change came from the remote device

    public ClipboardService(
        ILogger<ClipboardService> logger,
        ISessionManager sessionManager,
        IUserSettingsService userSettingsService,
        IPlatformNotificationHandler platformNotificationHandler,
        IDeviceManager deviceManager,
        IFileTransferService fileTransferService)
    {
        this.logger = logger;
        this.sessionManager = sessionManager;
        this.userSettingsService = userSettingsService;
        this.platformNotificationHandler = platformNotificationHandler;
        this.deviceManager = deviceManager;
        this.fileTransferService = fileTransferService;
        dispatcher = App.MainWindow?.DispatcherQueue 
            ?? throw new InvalidOperationException("MainWindow.Instance.DispatcherQueue is null");

        dispatcher.EnqueueAsync(() =>
        {
            try
            {
                Clipboard.ContentChanged += OnClipboardContentChanged;
                logger.LogInformation("Clipboard monitoring started");
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to start clipboard monitoring {ex}", ex);
            }
        });

        fileTransferService.FileReceived += async (sender, args) =>
        {
           await SetContentAsync(args.data, args.device);
        };
    }

    private async void OnClipboardContentChanged(object? sender, object? e)
    {
        if (isInternalUpdate)
            return;

        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                var dataPackageView = Clipboard.GetContent();
                if (dataPackageView == null) return;
                
                // Check if any connected devices have clipboard sync enabled
                var devicesWithClipboardSync = deviceManager.PairedDevices
                    .Where(device => device.Session != null && 
                                   device.DeviceSettings?.ClipboardSyncEnabled == true)
                    .ToList();
                
                if (devicesWithClipboardSync.Count == 0)
                {
                    logger.LogDebug("No connected devices have clipboard sync enabled, skipping clipboard processing");
                    return;
                }
                
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    await TryHandleTextContent(dataPackageView);
                }
                else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
                {
                    // Check if any device has image clipboard enabled
                    var devicesWithImageSync = devicesWithClipboardSync
                        .Where(device => device.DeviceSettings?.ImageToClipboardEnabled == true)
                        .ToList();
                    
                    if (devicesWithImageSync.Count != 0)
                    {
                        await TryHandleBitmapContent(dataPackageView);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling clipboard content");
            }
        });
    }

    private async Task<bool> TryHandleTextContent(DataPackageView dataPackageView)
    {
        if (!dataPackageView.Contains(StandardDataFormats.Text)) return false;

        string? text = await dataPackageView.GetTextAsync();
        logger.LogInformation("Clipboard content changed");
        if (string.IsNullOrEmpty(text)) return false;

        // Convert Windows CRLF to Unix LF 
        text = text.Replace("\r\n", "\n");
        
        var message = new ClipboardMessage
        {
            Content = text,
            ClipboardType = "text/plain"
        };

        var serializedMessage = SocketMessageSerializer.Serialize(message);
        
        // Send message only to devices that have clipboard sync enabled
        foreach (var device in deviceManager.PairedDevices)
        {
            if (device.Session != null && device.DeviceSettings?.ClipboardSyncEnabled == true)
            {
                sessionManager.SendMessage(device.Session, serializedMessage);
                logger.LogDebug("Sent clipboard message to device {DeviceId}", device.Id);
            }
        }
        
        return true;
    }

    private async Task<bool> TryHandleBitmapContent(DataPackageView dataPackageView)
    {
        if (!dataPackageView.Contains(StandardDataFormats.Bitmap)) return false;

        var imageStream = await dataPackageView.GetBitmapAsync();
        if (imageStream == null) return false;

        string mimeType = await DetermineImageMimeType(dataPackageView) ?? "image/png";
        await HandleImageTransfer(imageStream, mimeType);
        return true;
    }

    private async Task<string?> DetermineImageMimeType(DataPackageView dataPackageView)
    {
        if (!dataPackageView.AvailableFormats.Contains("UniformResourceLocatorW")) 
            return null;

        try
        {
            var urlData = await dataPackageView.GetDataAsync("UniformResourceLocatorW");
            var urlString = urlData?.ToString();
            if (string.IsNullOrEmpty(urlString)) return null;

            var extension = Path.GetExtension(urlString.Split('?')[0]).TrimStart('.');
            return !string.IsNullOrEmpty(extension) ? $"image/{extension.ToLowerInvariant()}" : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get URL data");
            return null;
        }
    }

    private async Task HandleImageTransfer(RandomAccessStreamReference imageStream, string mimeType)
    {
        using var dataStream = await imageStream.OpenReadAsync();
        using var compressed = await CompressImageStream(dataStream, mimeType);
        
        if (dataStream.Size > DirectTransferThreshold)
        {
            await HandleLargeImageTransfer(compressed, mimeType);
        }
        else
        {
            await HandleSmallImageTransfer(compressed, mimeType);
        }
    }

    private static async Task<Stream> CompressImageStream(IRandomAccessStream stream, string mimeType)
    {
        var compressedStream = new MemoryStream();
        
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var softwareBitmap = await decoder.GetSoftwareBitmapAsync();
        
        var encoder = await BitmapEncoder.CreateAsync(
            mimeType.Contains("png") ? BitmapEncoder.PngEncoderId : BitmapEncoder.JpegEncoderId, 
            compressedStream.AsRandomAccessStream());
            
        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();
        
        compressedStream.Position = 0;
        return compressedStream;
    }

    private async Task HandleLargeImageTransfer(Stream stream, string mimeType)
    {
        var fileName = $"clipboard_image_{DateTime.Now:yyyyMMddHHmmss}.{mimeType.Split('/').Last()}";
        var metadata = new FileMetadata
        {
            FileName = fileName,
            MimeType = mimeType,
            FileSize = stream.Length
        };
        foreach (var device in deviceManager.PairedDevices)
        {
            if (device.Session != null && 
                device.DeviceSettings.ClipboardSyncEnabled &&
                device.DeviceSettings.ImageToClipboardEnabled)
            {
                await fileTransferService.SendFileWithStream(stream, metadata, device);
            }
        }
    }

    private async Task HandleSmallImageTransfer(Stream stream, string mimeType)
    {
        byte[] buffer = new byte[stream.Length];
        await stream.ReadExactlyAsync(buffer);

        var message = new ClipboardMessage
        {
            Content = Convert.ToBase64String(buffer),
            ClipboardType = mimeType
        };

        var serializedMessage = SocketMessageSerializer.Serialize(message);
        
        // Send message only to devices that have clipboard sync enabled
        foreach (var device in deviceManager.PairedDevices)
        {
            if (device.Session != null && 
                device.DeviceSettings.ClipboardSyncEnabled &&
                device.DeviceSettings.ImageToClipboardEnabled)
            {
                sessionManager.SendMessage(device.Session, serializedMessage);
                logger.LogDebug("Sent clipboard message to device {DeviceId}", device.Id);
            }
        }
    }

    public async Task SetContentAsync(object content, PairedDevice sourceDevice)
    {
        if (dispatcher == null)
        {
            throw new InvalidOperationException("DispatcherQueue is not available");
        }

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
                            await platformNotificationHandler.ShowClipboardNotificationWithActions(
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
                    await platformNotificationHandler.ShowClipboardNotification(
                        "Clipboard data received",
                        $"Content type: {content.GetType().Name}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error setting clipboard content");
                throw;
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

    public void Dispose()
    {
        dispatcher?.TryEnqueue(() => Clipboard.ContentChanged -= OnClipboardContentChanged);
    }
}
