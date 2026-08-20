using Sefirah.Data.Models;

namespace Sefirah.Features;

public class PlaySoundFeature : IPlaySoundFeature
{
    private readonly Dictionary<string, CancellationTokenSource> playSoundCtsByDevice = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async void Toggle(PairedDevice device)
    {
        if (!device.IsConnected || device.IsPlayingSound)
            return;

        device.IsPlayingSound = true;
        device.SendMessage(new PlaySound { IsPlaying = true });

        CancelTimer(device.Id);
        var cts = new CancellationTokenSource();
        playSoundCtsByDevice[device.Id] = cts;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), cts.Token);
            Stop(device, notifyRemote: true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void HandleRemoteState(PairedDevice device, bool isPlaying)
    {
        if (isPlaying)
        {
            device.IsPlayingSound = true;
            return;
        }

        Stop(device, notifyRemote: false);
    }

    private void Stop(PairedDevice device, bool notifyRemote)
    {
        CancelTimer(device.Id);

        if (!device.IsPlayingSound)
            return;

        device.IsPlayingSound = false;
        if (notifyRemote && device.IsConnected)
            device.SendMessage(new PlaySound { IsPlaying = false });
    }

    private void CancelTimer(string deviceId)
    {
        if (!playSoundCtsByDevice.Remove(deviceId, out var cts))
            return;

        cts.Cancel();
        cts.Dispose();
    }
}
