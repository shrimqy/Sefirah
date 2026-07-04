using System.Runtime.InteropServices;

namespace Sefirah.Platforms.Windows.Interop;

/// <summary>Undocumented COM interface for changing the default audio endpoint.</summary>
[ComImport]
[Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[CoClass(typeof(PolicyConfigClient))]
internal interface IPolicyConfig
{
    [PreserveSig]
    int GetMixFormat(string pszDeviceName, out nint ppFormat);

    [PreserveSig]
    int GetDeviceFormat(string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, out nint ppFormat);

    [PreserveSig]
    int ResetDeviceFormat(string pszDeviceName);

    [PreserveSig]
    int SetDeviceFormat(string pszDeviceName, nint pEndpointFormat, nint mixFormat);

    [PreserveSig]
    int GetProcessingPeriod(string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);

    [PreserveSig]
    int SetProcessingPeriod(string pszDeviceName, long pmftPeriod);

    [PreserveSig]
    int GetShareMode(string pszDeviceName, out ERole pMode);

    [PreserveSig]
    int SetShareMode(string pszDeviceName, in ERole mode);

    [PreserveSig]
    int GetPropertyValue(string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, nint key, nint pv);

    [PreserveSig]
    int SetPropertyValue(string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bFxStore, nint key, nint pv);

    [PreserveSig]
    int SetDefaultEndpoint(string pszDeviceName, ERole role);

    [PreserveSig]
    int SetEndpointVisibility(string pszDeviceName, [MarshalAs(UnmanagedType.Bool)] bool bVisible);
}

[ComImport]
[Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
internal class PolicyConfigClient;

internal enum ERole
{
    eConsole = 0,
    eMultimedia = 1,
    eCommunications = 2
}
