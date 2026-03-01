using System.Collections.Concurrent;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetCoreServer;

namespace Sefirah.Helpers;

/// <summary>
/// Manages SSL/TLS certificates and provides SslContext.
/// </summary>
public static class SslHelper
{
    private static readonly TimeSpan StashTtl = TimeSpan.FromSeconds(30);

    private static readonly ConcurrentDictionary<string, ConcurrentQueue<(byte[] Cert, long Ticks)>> CertByPublicKey = new();
    private static readonly Timer StashCleanupTimer = new(_ => PurgeExpiredStash(), null, Timeout.Infinite, Timeout.Infinite);

    public static byte[] DevicePublicKeyEncoded => _devicePublicKeyEncoded ??= GetPublicKeyEncoded(GetOrCreateCertificate());
    private static byte[]? _devicePublicKeyEncoded;

    public static string DevicePublicKeyString => _devicePublicKeyString ??= GetPublicKeyStringBase64(DevicePublicKeyEncoded);
    private static string? _devicePublicKeyString;

    private static string GetPublicKeyStringBase64(byte[] encodedPublicKey)
    {
        return Convert.ToBase64String(encodedPublicKey);
    }

    private static byte[] GetPublicKeyEncoded(X509Certificate2 c)
    {
        using var ecdsa = c.GetECDsaPublicKey();
        if (ecdsa is not null) return ecdsa.ExportSubjectPublicKeyInfo();
        return [];
    }

    public static string GetVerificationCode(string theirPublicKeyBase64)
    {
        byte[] theirEncoded = Convert.FromBase64String(theirPublicKeyBase64); 

        if (theirEncoded.Length == 0) return "00000000";

        var concat = SortedConcatUnsigned(DevicePublicKeyEncoded, theirEncoded);
        return FormatVerificationCode(SHA256.HashData(concat));
    }

    /// <summary>concat in deterministic order (unsigned byte comparison). When a &lt; b returns b+a, else a+b.</summary>
    private static byte[] SortedConcatUnsigned(byte[] a, byte[] b)
    {
        return CompareUnsigned(a, b) < 0 ? [.. b, .. a] : [.. a, .. b];
    }

    private static int CompareUnsigned(byte[] a, byte[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            var u = a[i] & 0xFF;
            var v = b[i] & 0xFF;
            if (u != v) return u.CompareTo(v);
        }
        return a.Length.CompareTo(b.Length);
    }

    private static string FormatVerificationCode(byte[] hash)
    {
        var hex = Convert.ToHexString(hash).AsSpan(0, Math.Min(8, hash.Length * 2));
        return hex.ToString().ToUpperInvariant();
    }

    public static byte[]? GetCertForPublicKey(string? publicKeyString)
    {
        if (string.IsNullOrEmpty(publicKeyString) || !CertByPublicKey.TryGetValue(publicKeyString, out var queue))
            return null;

        if (!queue.TryDequeue(out var entry))
        {
            CertByPublicKey.TryRemove(publicKeyString, out _);
            return null;
        }

        if (queue.IsEmpty)
            CertByPublicKey.TryRemove(publicKeyString, out _);

        return entry.Cert;
    }

    private static void PurgeExpiredStash()
    {
        var now = Environment.TickCount64;
        foreach (var (key, queue) in CertByPublicKey)
        {
            while (queue.TryPeek(out var entry) && now - entry.Ticks > StashTtl.TotalMilliseconds)
                queue.TryDequeue(out _);

            if (queue.IsEmpty)
                CertByPublicKey.TryRemove(key, out _);
        }

        if (CertByPublicKey.IsEmpty)
            StashCleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private static string CertificateFileName { get; } = "Sefirah.pfx";

    private static X509Certificate2 CreateECDSACertificate()
    {
        // Create ECDSA with NIST P-256 curve
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(true);

        var subjectName = new X500DistinguishedName("CN=SefirahCastle");
        CertificateRequest certRequest = new(subjectName, ecdsa, HashAlgorithmName.SHA256);

        // Add certificate extensions
        certRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));

        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, true));

        // Create self-signed certificate valid for 10 years
        X509Certificate2 certificate = certRequest.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(10));

        // Ensure the certificate is exportable
        byte[] exportedData = certificate.Export(X509ContentType.Pfx);
        certificate = X509CertificateLoader.LoadPkcs12(exportedData, null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        string certPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, CertificateFileName);

        File.WriteAllBytes(certPath, exportedData);

        return certificate;
    }

    public static X509Certificate2 GetOrCreateCertificate()
    {
        string certPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, CertificateFileName);

        if (File.Exists(certPath))
        {
            try
            {
                return X509CertificateLoader.LoadPkcs12FromFile(certPath, null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load certificate: {ex.Message}");
            }
        }

        return CreateECDSACertificate();
    }

    public static SslContext GetSslContext()
    {
        return new SslContext(SslProtocols.Tls12 | SslProtocols.Tls13, GetOrCreateCertificate(), StashCertAndAccept)
        {
            ClientCertificateRequired = true
        };
    }

    /// <summary>SslContext which validates peer cert against expected cert.</summary>
    public static SslContext CreateSslContext(byte[] expectedPeerCert)
    {
        return new SslContext(SslProtocols.Tls12 | SslProtocols.Tls13, GetOrCreateCertificate(), Validate)
        {
            ClientCertificateRequired = true
        };

        bool Validate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
        {
            if (cert is null) return false;
            using var c = new X509Certificate2(cert);
            var raw = c.RawData;
            return raw.Length == expectedPeerCert.Length && raw.AsSpan().SequenceEqual(expectedPeerCert);
        }
    }

    private static bool StashCertAndAccept(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors)
    {
        if (cert is null) return false;
        using var c = new X509Certificate2(cert);
        var publicKeyString = GetPublicKeyStringBase64(GetPublicKeyEncoded(c));
        var queue = CertByPublicKey.GetOrAdd(publicKeyString, _ => new ConcurrentQueue<(byte[], long)>());
        queue.Enqueue((cert.GetRawCertData(), Environment.TickCount64));
        StashCleanupTimer.Change(StashTtl, StashTtl);
        return true;
    }
}
