using CommunityToolkit.WinUI;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Sefirah.App.Data.Contracts;
using Sefirah.App.Data.Models;
using Sefirah.App.Utils.Serialization;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using static Sefirah.App.Services.ToastNotificationService;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;

namespace Sefirah.App.Services;

public class ClipboardService : IClipboardService
{
    private readonly ILogger logger;
    private readonly ISessionManager sessionManager;
    private readonly IUserSettingsService userSettingsService;
    private readonly IFileTransferService fileTransferService;
    private readonly DispatcherQueue dispatcher;
    private const int DirectTransferThreshold = 512 * 512; // 1MB threshold
    
    private bool isInternalUpdate; // To track if the clipboard change came from the remote device

    public ClipboardService(
        ILogger logger,
        ISessionManager sessionManager,
        IUserSettingsService userSettingsService,
        IFileTransferService fileTransferService)
    {
        this.logger = logger;
        this.sessionManager = sessionManager;
        this.userSettingsService = userSettingsService;
        this.fileTransferService = fileTransferService;
        this.dispatcher = MainWindow.Instance?.DispatcherQueue 
            ?? throw new InvalidOperationException("MainWindow.Instance.DispatcherQueue is null");

        dispatcher.EnqueueAsync(() =>
        {
            Clipboard.ContentChanged += OnClipboardContentChanged;
            logger.Info("Clipboard monitoring started");
        });

        fileTransferService.FileReceived += async (sender, data) =>
        {
            await SetContentAsync(data);
        };  
    }

    private async void OnClipboardContentChanged(object? sender, object? e)
    {
        if (isInternalUpdate || !userSettingsService.FeatureSettingsService.ClipboardSyncEnabled)
        {
            return;
        }

        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                var dataPackageView = Clipboard.GetContent();
                if (dataPackageView == null) return;

                logger.Info($"Clipboard content changed: {string.Join(", ", dataPackageView.AvailableFormats)}");
                if (dataPackageView.Contains(StandardDataFormats.Text))
                {
                    await TryHandleTextContent(dataPackageView);
                }
                else if (dataPackageView.Contains(StandardDataFormats.Bitmap) && userSettingsService.FeatureSettingsService.ImageToClipboardEnabled)
                {
                    await TryHandleBitmapContent(dataPackageView);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error handling clipboard content: {Error}", ex);
            }
        });
    }

    private async Task<bool> TryHandleTextContent(DataPackageView dataPackageView)
    {
        if (!dataPackageView.Contains(StandardDataFormats.Text)) return false;

        string? text = await dataPackageView.GetTextAsync();
        if (string.IsNullOrEmpty(text)) return false;

        var message = new ClipboardMessage
        {
            Content = text,
            ClipboardType = "text/plain"
        };

        sessionManager.SendMessage(SocketMessageSerializer.Serialize(message));
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
            logger.Warn("Failed to get URL data: {Error}", ex.Message);
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

    private async Task<Stream> CompressImageStream(IRandomAccessStream stream, string mimeType)
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

        await fileTransferService.SendFile(stream, metadata);
    }

    private async Task HandleSmallImageTransfer(Stream stream, string mimeType)
    {
        byte[] buffer = new byte[stream.Length];
        await stream.ReadAsync(buffer);

        var message = new ClipboardMessage
        {
            Content = Convert.ToBase64String(buffer),
            ClipboardType = mimeType
        };

        sessionManager.SendMessage(SocketMessageSerializer.Serialize(message));
    }

    public async Task SetContentAsync(object content)
    {
        if (dispatcher == null)
        {
            throw new InvalidOperationException("DispatcherQueue is not available");
        }

        if (!userSettingsService.FeatureSettingsService.ClipboardSyncEnabled) return;

        await dispatcher.EnqueueAsync(async () =>
        {
            try
            {
                isInternalUpdate = true;
                var dataPackage = new DataPackage();
                var builder = new AppNotificationBuilder()
                    .AddText("Clipboard data received", new AppNotificationTextProperties());

                switch (content)
                {
                    case StorageFile file:
                        // Set package family name for proper file handling
                        dataPackage.Properties.PackageFamilyName = 
                            Windows.ApplicationModel.Package.Current.Id.FamilyName;
                        // Pass false as second parameter to indicate the app isn't taking ownership of the files
                        dataPackage.SetStorageItems([file], false);
                        break;
                    case string textContent:
                        dataPackage.SetText(textContent);
                        Uri.TryCreate(textContent, UriKind.Absolute, out Uri? uri);
                        bool isValidUri = IsValidWebUrl(uri);
                        if (userSettingsService.FeatureSettingsService.OpenLinksInBrowser && isValidUri)
                        {
                            await Launcher.LaunchUriAsync(uri);
                        }
                        else if (isValidUri && userSettingsService.FeatureSettingsService.ShowClipboardToast)
                        {
                            builder.AddButton(new AppNotificationButton("Open in browser")
                                .AddArgument("notificationType", ToastNotificationType.Clipboard)
                                .AddArgument("uri", textContent));
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unsupported content type: {content.GetType()}");
                }

                Clipboard.SetContent(dataPackage);
                await Task.Delay(50);
                logger.Info("Clipboard content set: {Content}", content);
                var notification = builder.BuildNotification();

                if (userSettingsService.FeatureSettingsService.ShowClipboardToast)
                {
                    notification.ExpiresOnReboot = true;
                    AppNotificationManager.Default.Show(notification);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error setting clipboard content: {Error}", ex);
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