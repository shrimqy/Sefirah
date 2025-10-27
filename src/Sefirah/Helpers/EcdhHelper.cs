using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Sefirah.Helpers;

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

    public static string GenerateNonce()
    {
        var nonce = new byte[32];
        new SecureRandom().NextBytes(nonce);
        return Convert.ToBase64String(nonce);
    }

    public static string GenerateProof(byte[] sharedSecret, string nonce)
    {
        var hmac = new Org.BouncyCastle.Crypto.Macs.HMac(new Sha256Digest());
        hmac.Init(new KeyParameter(sharedSecret));
        
        var nonceBytes = Convert.FromBase64String(nonce);
        hmac.BlockUpdate(nonceBytes, 0, nonceBytes.Length);
        
        var proof = new byte[hmac.GetMacSize()];
        hmac.DoFinal(proof, 0);
        
        return Convert.ToBase64String(proof);
    }

    public static bool VerifyProof(byte[] sharedSecret, string nonce, string proof)
    {
        var expectedProof = GenerateProof(sharedSecret, nonce);
        return expectedProof == proof;
    }

    /// <summary>
    /// Generates a random password with 12 characters containing uppercase letters, 
    /// lowercase letters, numbers, and special characters
    /// </summary>
    public static string GenerateRandomPassword()
    {
        const string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                                    "abcdefghijklmnopqrstuvwxyz" +
                                    "0123456789" +
                                    "!@#$%^&*";
        
        return new string(Enumerable.Range(1, 12)
            .Select(_ => allowedChars[Random.Shared.Next(allowedChars.Length)])
            .ToArray());
    }
}
