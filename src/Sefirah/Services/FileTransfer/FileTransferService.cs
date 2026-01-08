using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.Contracts;
using Sefirah.Data.Models;
using Sefirah.Dialogs;
using Sefirah.Helpers;

namespace Sefirah.Services.FileTransfer;

public class FileTransferService(
    ILogger logger,
    IUserSettingsService userSettingsService,
    IDeviceManager deviceManager,
    IPlatformNotificationHandler notificationHandler
    ) : IFileTransferService
{
    public const string CompleteMessage = "complete";
    public const string StartMessage = "start";
    public const int ChunkSize = 524288; // 512KB
    public static readonly IEnumerable<int> PortRange = Enumerable.Range(5152, 18);

    private readonly ConcurrentDictionary<Guid, IDisposable> activeHandlers = [];
    
    private string StorageLocation => userSettingsService.GeneralSettingsService.ReceivedFilesPath;

    public event EventHandler<(PairedDevice device, StorageFile data)>? FileReceived;

    public void CancelAllTransfers()
    {
        foreach (var handler in activeHandlers.Values)
        {
            switch (handler)
            {
                case ReceiveFileHandler receiveHandler:
                    receiveHandler.Cancel();
                    break;
                case SendFileHandler sendHandler:
                    sendHandler.Cancel();
                    break;
            }
        }
    }

    public void CancelTransfer(Guid guid)
    {
        if (activeHandlers.TryGetValue(guid, out var handler))
        {
            switch (handler)
            {
                case ReceiveFileHandler receiveHandler:
                    receiveHandler.Cancel();
                    break;
                case SendFileHandler sendHandler:
                    sendHandler.Cancel();
                    break;
            }
        }
    }

    #region Receive

    public async Task ReceiveFiles(FileTransferMessage data, PairedDevice device)
    {
        var handler = new ReceiveFileHandler(
            data.Files,
            data.ServerInfo,
            device,
            StorageLocation,
            logger,
            notificationHandler);

        try
        {
            var transferId = await handler.ConnectAsync();
            activeHandlers[transferId] = handler;

            var file = await handler.ReceiveAsync();

            if (file is not null && device.DeviceSettings.ClipboardFilesEnabled)
            {
                FileReceived?.Invoke(this, (device, file));
            }
        }
        finally
        {
            activeHandlers.TryRemove(handler.TransferId, out _);
            handler.Dispose();
        }
    }

    #endregion

    #region Send

    public async void SendFilesWithPicker(IReadOnlyList<IStorageItem> storageItems)
    {
        try
        {
            var files = storageItems.OfType<StorageFile>().ToArray();
            var devices = deviceManager.PairedDevices.Where(d => d.IsConnected).ToList();
            List<PairedDevice> selectedDevices = new();

            if (devices.Count == 0)
            {
                return;
            }
            else if (devices.Count == 1)
            {
                selectedDevices.Add(devices[0]);
            }
            else if (devices.Count > 1)
            {
                App.MainWindow.AppWindow.Show();
                App.MainWindow.Activate();
                selectedDevices = await ShowDeviceSelectionDialog(devices);
            }

            if (selectedDevices.Count == 0) return;

            await Task.Run(async () =>
            {
                foreach (var device in selectedDevices)
                {
                    await SendFiles(files, device);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error in sending files: {ex.Message}", ex);
        }
    }

    public async Task SendFiles(StorageFile[] files, PairedDevice device, bool isClipboard = false)
    {
        var fileMetadataList = await Task.WhenAll(files.Select(file => file.ToFileMetadata()));

        var handler = new SendFileHandler(
            files,
            fileMetadataList.ToList(),
            device,
            serverInfo => device.SendMessage(new FileTransferMessage
            {
                Files = [.. fileMetadataList],
                ServerInfo = serverInfo,
                IsClipboard = isClipboard
            }),
            logger,
            notificationHandler);

        try
        {
            var transferId = await handler.WaitForConnectionAsync();
            activeHandlers[transferId] = handler;
            
            await handler.SendAsync();
        }
        finally
        {
            activeHandlers.TryRemove(handler.TransferId, out _);
            handler.Dispose();
        }
    }

    private async Task<List<PairedDevice>> ShowDeviceSelectionDialog(List<PairedDevice> onlineDevices)
    {
        List<PairedDevice> selectedDevices = new();

        await App.MainWindow.DispatcherQueue.EnqueueAsync(async () =>
        {
            var deviceSelectorDialog = new DeviceSelectorDialog(onlineDevices);

            var dialog = new ContentDialog
            {
                XamlRoot = App.MainWindow.Content!.XamlRoot,
                Title = "SelectDevice".GetLocalizedResource(),
                Content = deviceSelectorDialog,
                PrimaryButtonText = "Start".GetLocalizedResource(),
                CloseButtonText = "Cancel".GetLocalizedResource(),
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                selectedDevices = deviceSelectorDialog.ViewModel.SelectedDevices;
            }
        });

        return selectedDevices;
    }

    #endregion
}
