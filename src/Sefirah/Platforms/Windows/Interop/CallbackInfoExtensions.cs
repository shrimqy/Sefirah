using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace Sefirah.Platforms.Windows.Interop;
public static class CallbackInfoExtensions
{
    public static CldApi.CF_OPERATION_INFO ToOperationInfo(this CldApi.CF_CALLBACK_INFO callbackInfo, CldApi.CF_OPERATION_TYPE operationType) =>
         new()
         {
             StructSize = (uint)Marshal.SizeOf<CldApi.CF_OPERATION_INFO>(),
             Type = operationType,
             ConnectionKey = callbackInfo.ConnectionKey,
             TransferKey = callbackInfo.TransferKey,
             RequestKey = callbackInfo.RequestKey,
         };
}
