using Windows.Foundation.Metadata;

namespace Sefirah.Platforms.Windows.Utilities;

internal static class CallingFeatureUtils
{
    private const string FeatureId = "com.microsoft.windows.applicationmodel.phonelinetransportdevice_v1";
    private const string FeatureKey = "cb9WIvVfhp+8lFhaSrB6V6zUBGqctteKi/f/9AIeoZ4";
    private const string CallsPhoneContract = "Windows.ApplicationModel.Calls.CallsPhoneContract";

    /// <summary>
    /// Transport pairing + outbound dial. Available from CallsPhoneContract v5 (Windows 10 1903+).
    /// </summary>
    internal static bool IsBluetoothCallingSupportedByPlatform() =>
        ApiInformation.IsApiContractPresent(CallsPhoneContract, 5);

    /// <summary>
    /// In-call control (PhoneCall, DialWithResultAsync, GetAllActivePhoneCallsAsync).
    /// Requires CallsPhoneContract v6 (build 20348+ / Windows 11).
    /// </summary>
    internal static bool SupportsCallControlApis() =>
        ApiInformation.IsApiContractPresent(CallsPhoneContract, 6);

    internal static bool TryUnlockPhoneLineTransportDeviceAPIs(ILogger logger) =>
        ShouldSkipLAFCheck() || TryUnlockPhoneLineTransportLimitedAccessFeature(logger);

    private static bool ShouldSkipLAFCheck() => WindowsVersion.Current.Major >= 22000;

    private static bool TryUnlockPhoneLineTransportLimitedAccessFeature(ILogger logger)
    {
        try
        {
            var token = FeatureTokenGenerator.GenerateTokenFromFeatureId(FeatureId, FeatureKey);
            var attestation = FeatureTokenGenerator.GenerateAttestation(FeatureId);
            var accessResult = LimitedAccessFeatures.TryUnlockFeature(FeatureId, token, attestation);
            if (accessResult is not null)
            {
                logger.Info($"Phone line transport LAF {FeatureId}: {accessResult.Status}");
                return accessResult.Status is LimitedAccessFeatureStatus.Available
                    || accessResult.Status is LimitedAccessFeatureStatus.AvailableWithoutToken;
            }
        }
        catch (Exception ex)
        {
            logger.Warn("TryUnlockPhoneLineTransportLimitedAccessFeature", ex);
        }

        return false;
    }
}
