namespace GrumPlates.Infrastructure.Hosting;

internal sealed class DataProtectionKeyStorageOptions
{
    public const string SectionName = "DataProtection";

    public string KeyEncryptionCertificatePath { get; init; } = string.Empty;
    public string KeyEncryptionCertificatePassword { get; init; } = string.Empty;
}
