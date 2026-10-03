using System.Collections.Concurrent;
using CommunityToolkit.WinUI;
using Sefirah.Data.Models;
using Sefirah.Dialogs;
using Sefirah.Helpers;
using Sefirah.Utils;

namespace Sefirah.Services.Transfer;

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

    public async Task<StorageFile?> Receive(FileTransferSession session, PairedDevice device, bool silent = false)
    {
        if (device.Certificate is null || device.Certificate.Length == 0)
        {
            logger.Error("Cannot receive files: device has no pinned certificate. Re-pair the device.");
            return null;
        }

        var savePath = silent
            ? LocalAppPaths.GetClipboardFolder()
            : StorageLocation;

        var handler = new ReceiveFileHandler(
            session.Files,
            session.ServerInfo,
            device,
            device.Certificate,
            savePath,
            silent,
            logger,
            notificationHandler);

        try
        {
            var transferId = await handler.ConnectAsync();
            activeHandlers[transferId] = handler;

            return await handler.ReceiveAsync();
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
            List<PairedDevice> selectedDevices = [];

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
                    await Send(files, device);
                }
            });
        }
        catch (Exception ex)
        {
            logger.Error($"Error in sending files: {ex.Message}", ex);
        }
    }

    public async Task Send(StorageFile[] files, PairedDevice device, bool silent = false)
    {
        if (device.Certificate is null || device.Certificate.Length == 0)
        {
            logger.Error("Cannot send files: device has no pinned certificate. Re-pair the device.");
            return;
        }

        var metadata = await Task.WhenAll(files.Select(file => file.ToFileMetadata()));

        var handler = new SendFileHandler(
            files,
            metadata,
            device,
            device.Certificate,
            serverInfo =>
            {
                var session = new FileTransferSession { Files = [.. metadata], ServerInfo = serverInfo };
                device.SendMessage(silent
                    ? new ClipboardTransfer { Transfer = session }
                    : new ShareTransfer { Transfer = session });
            },
            logger,
            notificationHandler,
            silent);

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

    private static async Task<List<PairedDevice>> ShowDeviceSelectionDialog(List<PairedDevice> onlineDevices)
    {
        List<PairedDevice> selectedDevices = [];

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

            if (result is ContentDialogResult.Primary)
            {
                selectedDevices = deviceSelectorDialog.ViewModel.SelectedDevices;
            }
        });

        return selectedDevices;
    }

    #endregion
}
