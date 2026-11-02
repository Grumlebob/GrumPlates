using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GrumPlates.Test.TestSupport.Accounts;

/// <summary>A self-signed RSA 2048 PFX for Data Protection key encryption, deleted on dispose.</summary>
internal sealed class TestKeyEncryptionCertificate : IDisposable
{
    public const string Password = "test-key-encryption-password";
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("GrumPlates-dp-cert-");

    public string Path { get; }

    public TestKeyEncryptionCertificate()
    {
        Path = System.IO.Path.Combine(_directory.FullName, "data-protection.pfx");
        using var rsa = RSA.Create(2048);
        using var certificate = new CertificateRequest(
                $"CN=GrumPlates test data protection {Guid.NewGuid():N}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(5));
        File.WriteAllBytes(Path, certificate.Export(X509ContentType.Pfx, Password));
    }

    // This exact directory was created by this instance and never shared.
    public void Dispose() => _directory.Delete(recursive: true);
}
