using GrumPlates.Infrastructure.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace GrumPlates.Test.Infrastructure.Hosting;

public sealed class DataProtectionKeyEncryptionTests
{
    [Fact]
    public void NonWindowsDevelopment_WithoutCertificate_StoresKeysUnencrypted()
    {
        var services = new ServiceCollection();

        AppCachingExtensions.ConfigureKeyEncryption(
            services.AddDataProtection(), Configuration(), Environment(Environments.Development), services, isWindows: false);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(System.Security.Cryptography.X509Certificates.X509Certificate2));
    }

    [Theory]
    [InlineData("Docker")]
    [InlineData("Production")]
    public void OutsideDevelopment_WithoutCertificate_FailsClosed(string environmentName)
    {
        var services = new ServiceCollection();

        var error = Assert.Throws<InvalidOperationException>(() => AppCachingExtensions.ConfigureKeyEncryption(
            services.AddDataProtection(), Configuration(), Environment(environmentName), services, isWindows: false));

        Assert.Contains("DataProtection:KeyEncryptionCertificatePath", error.Message, StringComparison.Ordinal);
    }

    // Empty values override any Kestrel certificate variables on the test machine.
    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataProtection:KeyEncryptionCertificatePath"] = "",
            ["DataProtection:KeyEncryptionCertificatePassword"] = ""
        })
        .Build();

    private static HostingEnvironment Environment(string name) => new() { EnvironmentName = name };
}
