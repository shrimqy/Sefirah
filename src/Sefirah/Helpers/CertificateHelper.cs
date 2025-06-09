using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Sefirah.Helpers;

public class CertificateHelper
{
    private static string CertificateFileName { get; } = "Sefirah.pfx";
    public static async Task<X509Certificate2> CreateECDSACertificate()
    {
        // Create ECDSA with NIST P-256 curve
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(true);

        var subjectName = new X500DistinguishedName("CN=SefirahCastle");
        CertificateRequest certRequest = new(subjectName, ecdsa, HashAlgorithmName.SHA256);

        // Add certificate extensions
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(true, false, 0, true));

        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));

        // Create self-signed certificate valid for 10 years
        X509Certificate2 certificate = certRequest.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(10));

        // Ensure the certificate is exportable
        certificate = new X509Certificate2(
            certificate.Export(X509ContentType.Pfx),
            password: null as SecureString,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        string certPath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path,
            CertificateFileName);

        // Export and save
        byte[] certData = certificate.Export(X509ContentType.Pfx);
        await File.WriteAllBytesAsync(certPath, certData);
        
        Debug.WriteLine($"Certificate saved to: {certPath}");
        return certificate;
    }

    public static async Task<X509Certificate2> GetOrCreateCertificateAsync()
    {
        string certPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, CertificateFileName);

        if (File.Exists(certPath))
        {
            try
            {
                return new X509Certificate2(certPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to load certificate: {ex.Message}");
            }
        }

        return await CreateECDSACertificate();
    }
}

