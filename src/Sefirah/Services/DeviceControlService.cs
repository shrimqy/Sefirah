using CommunityToolkit.WinUI;
using Sefirah.Data.Models;

namespace Sefirah.Services;

/// <summary>Target-side owner for capability publication and opt-in Bluetooth requests.</summary>
public sealed class DeviceControlService(IDeviceManager devices)
{
    private int radioSubscribed;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<(string Peer, string Id), (string Control, int Value, DeviceControlResult Result)> recent = [];

    public async Task<DeviceControls> GetCapabilitiesAsync(PairedDevice peer)
    {
        var result = new DeviceControls();
#if WINDOWS
        var radio = Ioc.Default.GetRequiredService<Platforms.Windows.Bluetooth.BluetoothRadioManager>();
        if (await radio.RefreshAsync() && radio.IsBluetoothSupported)
        {
            if (Interlocked.Exchange(ref radioSubscribed, 1) == 0)
                radio.RadioStateChanged += _ => PublishAll();
            result.BluetoothEnabled = radio.RadioState switch
            {
                Windows.Devices.Radios.RadioState.On => true,
                Windows.Devices.Radios.RadioState.Off => false,
                _ => null
            };
            result.BluetoothControl = peer.DeviceSettings.AllowRemoteBluetoothControl ? "direct" : "denied";
        }
#endif
        return result;
    }

    private async void PublishAll()
    {
        foreach (var peer in devices.PairedDevices.ToArray())
        {
            if (peer.Controls?.Version != 1) continue;
            try { await PublishAsync(peer); } catch (Exception) { /* A disconnected peer will refresh next connection. */ }
        }
    }

    public async Task PublishAsync(PairedDevice peer)
    {
        if (!peer.IsConnected || peer.Controls?.Version != 1) return;
        var local = await devices.GetLocalDeviceAsync();
        peer.SendMessage(new DeviceInfo { DeviceName = local.DeviceName, Controls = await GetCapabilitiesAsync(peer) });
    }

    public async Task HandleAsync(PairedDevice peer, DeviceControlRequest request)
    {
        // Receipt and reply are both gated: an old peer must never see an unknown discriminator.
        if (!peer.IsConnected || peer.Controls?.Version != 1) return;
        await gate.WaitAsync();
        try
        {
            if (!peer.IsConnected || peer.Controls?.Version != 1) return;
            if (!Guid.TryParse(request.RequestId, out _)) return;
            var key = (peer.Id, request.RequestId);
            if (recent.TryGetValue(key, out var previous))
            {
                peer.SendMessage(previous.Control == request.Control && previous.Value == request.Value
                    ? previous.Result : new DeviceControlResult { RequestId = request.RequestId, Status = "invalid" });
                return;
            }
            // Reserve the ID before any OS prompt; even a timed-out request is never replayed.
            if (recent.Count >= 128) recent.Remove(recent.Keys.First());
            recent[key] = (request.Control, request.Value,
                new DeviceControlResult { RequestId = request.RequestId, Status = "unconfirmed" });
            var status = "unsupported";
            if (request.Control == "bluetooth" && request.Value is 0 or 1)
            {
                status = "denied";
                if (peer.DeviceSettings.AllowRemoteBluetoothControl)
                {
#if WINDOWS
                    var radio = Ioc.Default.GetRequiredService<Platforms.Windows.Bluetooth.BluetoothRadioManager>();
                    // RequestAccessAsync may display consent, so invoke from the target's UI thread.
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    if (!App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
                    {
                        try { completion.TrySetResult(await radio.TrySetEnabledAsync(request.Value == 1, timeout.Token)); }
                        catch (Exception) { completion.TrySetResult(false); }
                    })) completion.TrySetResult(false);
                    status = await completion.Task.WaitAsync(timeout.Token) ? "applied" : "unconfirmed";
#else
                    status = "unsupported";
#endif
                }
            }
            else if (request.Control == "bluetooth") status = "invalid";
            var result = new DeviceControlResult { RequestId = request.RequestId, Status = status };
            recent[key] = (request.Control, request.Value, result);
            if (peer.IsConnected) peer.SendMessage(result);
            await PublishAsync(peer);
        }
        catch (OperationCanceledException)
        {
            if (peer.IsConnected) peer.SendMessage(new DeviceControlResult { RequestId = request.RequestId, Status = "unconfirmed" });
        }
        finally { gate.Release(); }
    }
}
