using System.Security.Cryptography.X509Certificates;
using GrumPlates.Infrastructure.Hosting.CacheInvalidation;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using StackExchange.Redis;

namespace GrumPlates.Infrastructure.Hosting;

internal static class AppCachingExtensions
{
    public static IServiceCollection AddAppCachingAndDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        IHealthChecksBuilder healthChecks)
    {
        var redisConnection = configuration.GetSection("Redis").GetValue<string>("Configuration");
        var hasRedis = !AppOptionsExtensions.IsPlaceholder(redisConnection);
        var allowMissingRedis = AllowMissingRedis(configuration, environment);
        services.AddOptions<DataProtectionKeyStorageOptions>()
            .Bind(configuration.GetSection(DataProtectionKeyStorageOptions.SectionName));

        if (!hasRedis && !allowMissingRedis)
        {
            throw new InvalidOperationException(
                "Redis:Configuration must be configured outside development/test environments. " +
                "Set Redis:AllowMissing=true only for deliberate local or test fallback.");
        }

        IConnectionMultiplexer? redis = null;
        if (hasRedis)
        {
            try
            {
                redis = ConnectionMultiplexer.Connect(redisConnection!);
            }
            catch (Exception ex) when (allowMissingRedis)
            {
                hasRedis = false;
                Log.Warning(ex, "Redis is configured but unavailable; using local development cache fallbacks.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Redis is required outside development/test environments but the configured Redis endpoint is unavailable. " +
                    "Fix Redis:Configuration or set Redis:AllowMissing=true only for deliberate local or test fallback.",
                    ex);
            }
        }

        if (redis is not null)
        {
            services.AddSingleton(redis);
            services.AddStackExchangeRedisCache(options =>
            {
                options.ConnectionMultiplexerFactory = () => Task.FromResult<IConnectionMultiplexer>(redis);
            });
            healthChecks.AddCheck<RedisHealthCheck>("redis", tags: ["ready"]);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        var appName = configuration.GetValue<string>("App:Name") ?? "GrumPlates";
        var dataProtection = services.AddDataProtection()
            .SetApplicationName(appName);

        dataProtection.PersistKeysToDbContext<AppDbContext>();
        ConfigureKeyEncryption(dataProtection, configuration, environment, services, OperatingSystem.IsWindows());

        services.AddHybridCache(options =>
        {
            options.MaximumKeyLength = 1024;
            options.MaximumPayloadBytes = 1024 * 1024;
            // Tag metrics add a dimension per cache tag; keep metric cardinality bounded.
            options.ReportTagMetrics = false;
        });
        services.AddAppCacheInvalidation(configuration, environment, hasRedis);
        return services;
    }

    private static bool AllowMissingRedis(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment()
        || environment.IsEnvironment("Testing")
        || configuration.GetSection("Redis").GetValue<bool>("AllowMissing");

    // isWindows is a parameter so tests can cover the non-Windows Development fallback on any OS.
    internal static void ConfigureKeyEncryption(
        IDataProtectionBuilder dataProtection,
        IConfiguration configuration,
        IHostEnvironment environment,
        IServiceCollection services,
        bool isWindows)
    {
        // OperatingSystem.IsWindows() also tells the platform analyzer that DPAPI is safe here.
        if (isWindows && OperatingSystem.IsWindows() && environment.IsDevelopment())
        {
            dataProtection.ProtectKeysWithDpapi();
            return;
        }

        var certificatePath = configuration[$"{DataProtectionKeyStorageOptions.SectionName}:KeyEncryptionCertificatePath"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Path");
        var certificatePassword = configuration[$"{DataProtectionKeyStorageOptions.SectionName}:KeyEncryptionCertificatePassword"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_Kestrel__Certificates__Default__Password");

        if (string.IsNullOrWhiteSpace(certificatePath) && environment.IsDevelopment())
        {
            Log.Warning("Data Protection keys are stored unencrypted because no key-encryption certificate is configured. This is allowed only in Development.");
            return;
        }

        if (string.IsNullOrWhiteSpace(certificatePath) || !File.Exists(certificatePath))
        {
            throw new InvalidOperationException(
                "Data Protection requires DataProtection:KeyEncryptionCertificatePath and its password outside Development.");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = Path.GetExtension(certificatePath).Equals(".pfx", StringComparison.OrdinalIgnoreCase)
                ? X509CertificateLoader.LoadPkcs12FromFile(
                    certificatePath,
                    certificatePassword,
                    X509KeyStorageFlags.EphemeralKeySet)
                : X509Certificate2.CreateFromEncryptedPemFile(certificatePath, certificatePassword ?? string.Empty);
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                "The configured Data Protection key-encryption certificate could not be loaded.");
        }

        if (!certificate.HasPrivateKey)
        {
            certificate.Dispose();
            throw new InvalidOperationException(
                "The configured Data Protection key-encryption certificate must include its private key.");
        }

        services.AddSingleton<X509Certificate2>(_ => certificate);
        dataProtection
            .ProtectKeysWithCertificate(certificate)
            .UnprotectKeysWithAnyCertificate(certificate);
    }
}
