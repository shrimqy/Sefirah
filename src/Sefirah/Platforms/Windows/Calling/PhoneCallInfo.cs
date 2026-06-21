using WinRTPhoneCallInfo = Windows.ApplicationModel.Calls.PhoneCallInfo;

namespace Sefirah.Platforms.Windows.Calling;

internal sealed class PhoneCallInfo(WinRTPhoneCallInfo info) : IPhoneCallInfo
{
    public string DisplayName => info.DisplayName;
    public string PhoneNumber => info.PhoneNumber;
}
