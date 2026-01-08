using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NetCoreServer;

namespace Sefirah.Helpers;

/// <summary>
/// Manages SSL/TLS certificates and provides SslContext
/// </summary>
public class CertificateHelper
{
    private static string CertificateFileName { get; } = "Sefirah.pfx";
    
    private static X509Certificate2 CreateECDSACertificate()
    {
        // Create ECDSA with NIST P-256 curve
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(true);

        var subjectName = new X500DistinguishedName("CN=SefirahCastle");
        CertificateRequest certRequest = new(subjectName, ecdsa, HashAlgorithmName.SHA256);

        // Add certificate extensions
        certRequest.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));

        certRequest.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment, true));

        // Create self-signed certificate valid for 10 years
        X509Certificate2 certificate = certRequest.CreateSelfSigned(
            DateTimeOffset.Now,
            DateTimeOffset.Now.AddYears(10));

        // Ensure the certificate is exportable
        byte[] exportedData = certificate.Export(X509ContentType.Pfx);
        certificate = X509CertificateLoader.LoadPkcs12(exportedData, null, 
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

        string certPath = Path.Combine(
            ApplicationData.Current.LocalFolder.Path,
            CertificateFileName);

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

    /// <summary>
    /// Cached SslContext for TLS connections (created once, reused everywhere)
    /// </summary>
    public static SslContext SslContext { get; } = CreateSslContext();

    private static SslContext CreateSslContext()
    {
        var certificate = GetOrCreateCertificate();
        return new SslContext(
            SslProtocols.Tls12 | SslProtocols.Tls13, 
            certificate, 
            (sender, cert, chain, errors) => true);
    }
}

