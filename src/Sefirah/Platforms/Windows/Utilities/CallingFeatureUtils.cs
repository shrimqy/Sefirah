using Windows.Foundation.Metadata;

namespace Sefirah.Platforms.Windows.Utilities;

internal static class CallingFeatureUtils
{
    private const string FeatureId = "com.microsoft.windows.applicationmodel.phonelinetransportdevice_v1";
    private const string FeatureKey = "cb9WIvVfhp+8lFhaSrB6V6zUBGqctteKi/f/9AIeoZ4";

    internal static bool IsBluetoothCallingSupportedByPlatform() =>
        ApiInformation.IsApiContractPresent("Windows.ApplicationModel.Calls.CallsPhoneContract", 5);

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
                logger.LogInformation("Phone line transport LAF {FeatureId}: {Status}", FeatureId, accessResult.Status);
                return accessResult.Status is LimitedAccessFeatureStatus.Available
                    || accessResult.Status is LimitedAccessFeatureStatus.AvailableWithoutToken;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TryUnlockPhoneLineTransportLimitedAccessFeature");
        }

        return false;
    }
}
