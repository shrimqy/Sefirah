using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Sefirah.App.Helpers;

public class EcdhHelper
{
    // ECDH key pair generator
    public static AsymmetricCipherKeyPair GetKeyPair()
    {
        var ecParams = SecNamedCurves.GetByName("secp256r1");
        var ecDomainParameters = new ECDomainParameters(ecParams.Curve, ecParams.G, ecParams.N, ecParams.H);

        var keyPairGenerator = new ECKeyPairGenerator();
        var keyGenParams = new ECKeyGenerationParameters(ecDomainParameters, new SecureRandom());
        keyPairGenerator.Init(keyGenParams);
        return keyPairGenerator.GenerateKeyPair();
    }

    public static byte[] DeriveKey(string androidPublicKey, byte[] privateKey)
    {
        // Reconstruct the key pair
        var ecParams = SecNamedCurves.GetByName("secp256r1");
        var ecDomainParameters = new ECDomainParameters(ecParams.Curve, ecParams.G, ecParams.N, ecParams.H);

        var privateKeyParameters = new ECPrivateKeyParameters(
            new Org.BouncyCastle.Math.BigInteger(1, privateKey),
            ecDomainParameters);
        byte[] rawPointBytes = Convert.FromBase64String(androidPublicKey);
        var point = ecParams.Curve.DecodePoint(rawPointBytes);
        var publicKeyParameters = new ECPublicKeyParameters(point,
            new ECDomainParameters(ecParams.Curve, ecParams.G, ecParams.N, ecParams.H));

        var agreement = AgreementUtilities.GetBasicAgreement("ECDH");
        agreement.Init(privateKeyParameters);
        var sharedSecret = agreement.CalculateAgreement(publicKeyParameters);
        var sharedSecretBytes = sharedSecret.ToByteArrayUnsigned();

        var sha256 = new Sha256Digest();
        var hashedSecret = new byte[sha256.GetDigestSize()];
        sha256.BlockUpdate(sharedSecretBytes, 0, sharedSecretBytes.Length);
        sha256.DoFinal(hashedSecret, 0);

        return hashedSecret;
    }

    public static bool VerifyDevice(string androidPublicKey, byte[] localPrivateKey, byte[] expectedHashedSecret)
    {
        var derivedHashedSecret = DeriveKey(androidPublicKey, localPrivateKey);
        if (derivedHashedSecret == null)
        {
            return false;
        }
        return derivedHashedSecret.SequenceEqual(expectedHashedSecret);
    }
}
