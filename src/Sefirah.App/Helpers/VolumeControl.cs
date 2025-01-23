using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Media.Devices;

namespace Sefirah.App.Helpers;

internal enum HResult : uint
{
    S_OK = 0
}

public static class VolumeControl
{
    public static float GetMasterVolume()
    {
        var masterVol = GetAudioEndpointVolumeInterface();
        // Make sure that the audio is not muted
        masterVol.SetMute(false, Guid.Empty);

        // Only adapt volume if the current level is below the specified minimum level
        float volume = masterVol.GetMasterVolumeLevelScalar();
        return volume;
    }


    public static void ChangeVolume(double level)
    {
        var volume = level / 100;

        try
        {
            float newAudioValue = Convert.ToSingle(volume);
            var masterVol = GetAudioEndpointVolumeInterface();
            if (masterVol == null)
                return;
            masterVol.SetMasterVolumeLevelScalar(newAudioValue, Guid.Empty);
        }
        catch { }
    }

    private static IAudioEndpointVolume GetAudioEndpointVolumeInterface()
    {
        var speakerId = MediaDevice.GetDefaultAudioRenderId(AudioDeviceRole.Default);
        var completionHandler = new ActivateAudioInterfaceCompletionHandler<IAudioEndpointVolume>();

        var hr = ActivateAudioInterfaceAsync(
            speakerId,
            typeof(IAudioEndpointVolume).GetTypeInfo().GUID,
            nint.Zero,
            completionHandler,
            out var activateOperation);

        Debug.Assert(hr == (uint)HResult.S_OK);

        return completionHandler.WaitForCompletion();
    }

    [DllImport("Mmdevapi.dll", ExactSpelling = true, PreserveSig = false)]
    [return: MarshalAs(UnmanagedType.Error)]
    private static extern uint ActivateAudioInterfaceAsync(
            [In, MarshalAs(UnmanagedType.LPWStr)] string deviceInterfacePath,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [In] nint activationParams,
            [In] IActivateAudioInterfaceCompletionHandler completionHandler,
            out IActivateAudioInterfaceAsyncOperation activationOperation);

    internal class ActivateAudioInterfaceCompletionHandler<T> : IActivateAudioInterfaceCompletionHandler
    {
        private AutoResetEvent _completionEvent;
        private T _result;

        public ActivateAudioInterfaceCompletionHandler()
        {
            _completionEvent = new AutoResetEvent(false);
        }

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            operation.GetActivateResult(out var hr, out var activatedInterface);

            Debug.Assert(hr == (uint)HResult.S_OK);

            _result = (T)activatedInterface;

            var setResult = _completionEvent.Set();
            Debug.Assert(setResult != false);
        }

        public T WaitForCompletion()
        {
            var waitResult = _completionEvent.WaitOne();
            Debug.Assert(waitResult != false);

            return _result;
        }
    }
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    void NotImpl1();

    void NotImpl2();

    [return: MarshalAs(UnmanagedType.U4)]
    uint GetChannelCount();

    void SetMasterVolumeLevel(
        [In][MarshalAs(UnmanagedType.R4)] float level,
        [In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    void SetMasterVolumeLevelScalar(
        [In][MarshalAs(UnmanagedType.R4)] float level,
        [In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    [return: MarshalAs(UnmanagedType.R4)]
    float GetMasterVolumeLevel();

    [return: MarshalAs(UnmanagedType.R4)]
    float GetMasterVolumeLevelScalar();

    void SetChannelVolumeLevel(
        [In][MarshalAs(UnmanagedType.U4)] uint channelNumber,
        [In][MarshalAs(UnmanagedType.R4)] float level,
        [In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    void SetChannelVolumeLevelScalar(
        [In][MarshalAs(UnmanagedType.U4)] uint channelNumber,
        [In][MarshalAs(UnmanagedType.R4)] float level,
        [In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    void GetChannelVolumeLevel(
        [In][MarshalAs(UnmanagedType.U4)] uint channelNumber,
        [Out][MarshalAs(UnmanagedType.R4)] out float level);

    [return: MarshalAs(UnmanagedType.R4)]
    float GetChannelVolumeLevelScalar([In][MarshalAs(UnmanagedType.U4)] uint channelNumber);

    void SetMute(
        [In][MarshalAs(UnmanagedType.Bool)] bool isMuted,
        [In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    [return: MarshalAs(UnmanagedType.Bool)]
    bool GetMute();

    void GetVolumeStepInfo(
        [Out][MarshalAs(UnmanagedType.U4)] out uint step,
        [Out][MarshalAs(UnmanagedType.U4)] out uint stepCount);

    void VolumeStepUp([In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    void VolumeStepDown([In][MarshalAs(UnmanagedType.LPStruct)] Guid eventContext);

    [return: MarshalAs(UnmanagedType.U4)] // bit mask
    uint QueryHardwareSupport();

    void GetVolumeRange(
        [Out][MarshalAs(UnmanagedType.R4)] out float volumeMin,
        [Out][MarshalAs(UnmanagedType.R4)] out float volumeMax,
        [Out][MarshalAs(UnmanagedType.R4)] out float volumeStep);
}

[ComImport]
[Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActivateAudioInterfaceAsyncOperation
{
    void GetActivateResult(
        [MarshalAs(UnmanagedType.Error)] out uint activateResult,
        [MarshalAs(UnmanagedType.IUnknown)] out object activatedInterface);
}

[ComImport]
[Guid("41D949AB-9862-444A-80F6-C261334DA5EB")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IActivateAudioInterfaceCompletionHandler
{
    void ActivateCompleted(IActivateAudioInterfaceAsyncOperation activateOperation);
}
