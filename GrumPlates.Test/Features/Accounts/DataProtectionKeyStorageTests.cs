using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using GrumPlates.Features.Accounts.Email;
using GrumPlates.Infrastructure.Persistence;
using GrumPlates.Test.TestSupport.Accounts;
using GrumPlates.Test.TestSupport.Integration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ApplicationUser = GrumPlates.Features.Accounts.Identity.ApplicationUser;

namespace GrumPlates.Test.Features.Accounts;

[Collection(TestCollectionNames.StartupIntegration)]
public sealed class DataProtectionKeyStorageTests
{
    private const string CertificatePassword = "fixture-key-encryption-password";

    [Fact]
    public async Task EnabledAccounts_FailClosedWhenCertificateIsMissing()
    {
        await using var fixture = new RealCookieAccountFixture(
            new Dictionary<string, string?>
            {
                ["DataProtection:KeyEncryptionCertificatePath"] = string.Empty,
                ["DataProtection:KeyEncryptionCertificatePassword"] = string.Empty
            });

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.InitializeAsync());
        Assert.Contains("DataProtection:KeyEncryptionCertificatePath", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task EnabledAccounts_EncryptPostgreSqlKeysWithEncryptedPemCertificate()
    {
        var appName = $"encrypted-pem-{Guid.NewGuid():N}";
        using var certificate = new FixturePemCertificate();
        await using var fixture = new RealCookieAccountFixture(
            new Dictionary<string, string?>
            {
                ["DataProtection:KeyEncryptionCertificatePath"] = certificate.Path,
                ["DataProtection:KeyEncryptionCertificatePassword"] = CertificatePassword
            },
            appName: appName);
        await fixture.InitializeAsync();

        var provider = fixture.Factory.Services.GetRequiredService<IDataProtectionProvider>();
        var protector = provider.CreateProtector("encrypted-pem-fixture");
        var payload = protector.Protect("encrypted PEM keys work");
        Assert.Equal("encrypted PEM keys work", protector.Unprotect(payload));
        var rows = await ReadKeyRowsAsync(fixture);
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains("encryptedSecret", row.Xml, StringComparison.Ordinal));
    }

    internal static async Task<List<(Guid KeyId, string Xml)>> ReadKeyRowsAsync(RealCookieAccountFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.DataProtectionKeys.AsNoTracking().ToListAsync();
        return rows.Select(row => (KeyId: (Guid)XElement.Parse(row.Xml!).Attribute("id")!, row.Xml!)).ToList();
    }

    private sealed class FixturePemCertificate : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("GrumPlates-dp-pem-cert-");
        public string Path { get; }

        public FixturePemCertificate()
        {
            Path = System.IO.Path.Combine(_directory.FullName, "fixture.pem");
            using var rsa = RSA.Create(2048);
            using var certificate = new CertificateRequest(
                    "CN=GrumPlates encrypted PEM fixture",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(5));
            var encryptedKey = rsa.ExportEncryptedPkcs8PrivateKey(
                CertificatePassword.AsSpan(),
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
            var privateKeyPem = "-----BEGIN ENCRYPTED PRIVATE KEY-----\n"
                + Convert.ToBase64String(encryptedKey, Base64FormattingOptions.InsertLineBreaks)
                + "\n-----END ENCRYPTED PRIVATE KEY-----\n";
            File.WriteAllText(Path, certificate.ExportCertificatePem() + "\n" + privateKeyPem);
            using var loaded = X509Certificate2.CreateFromEncryptedPemFile(Path, CertificatePassword);
            if (!loaded.HasPrivateKey)
            {
                throw new InvalidOperationException("Synthetic encrypted PEM certificate did not include a private key.");
            }
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}

[Collection(TestCollectionNames.CrossNodeRedis)]
public sealed class DataProtectionDatabaseRestoreTests : IClassFixture<SharedIntegrationEnvironment>
{
    private const string CertificatePassword = "fixture-key-encryption-password";
    private readonly SharedIntegrationEnvironment environment;
    private readonly ITestOutputHelper output;

    public DataProtectionDatabaseRestoreTests(SharedIntegrationEnvironment environment, ITestOutputHelper output)
    {
        this.environment = environment;
        this.output = output;
    }

    [Fact]
    public async Task DatabaseRestoreRecoversCookieStampAndEncryptedOutboxWithSameCertificate()
    {
        await ResetSharedDatabaseAsync();
        var appName = $"dp-restore-{Guid.NewGuid():N}";
        var targetDatabase = $"restore_{Guid.NewGuid():N}";
        using var certificate = new RestoreCertificate();
        using var temporary = new TemporaryDirectory();
        var dump = Path.Combine(temporary.FullName, "app.dump");
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(environment.PostgresConnectionString);
        var sourceDatabase = connection.Database ?? throw new InvalidOperationException("Fixture database name is missing.");
        var configuration = new Dictionary<string, string?>
        {
            ["DataProtection:KeyEncryptionCertificatePath"] = certificate.Path,
            ["DataProtection:KeyEncryptionCertificatePassword"] = CertificatePassword
        };
        var clock = new AccountTestClock();
        string cookiePair;
        (string Id, string Email, string Password) user;
        (string Id, string Email, string Password) staleMailUser;
        (string Id, string Email, string Password) expiredMailUser;
        string[] sourceMigrations;

        await using (var first = new RealCookieAccountFixture(
            configuration,
            configureTestServices: services => services.PostConfigure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(
                options => options.ValidationInterval = TimeSpan.Zero),
            appName: appName,
            postgresConnectionString: environment.PostgresConnectionString,
            clock: clock,
            redisConnectionString: environment.RedisConnectionString))
        {
            await first.InitializeAsync();
            user = await first.CreateUserAsync();
            staleMailUser = await first.CreateUserAsync();
            expiredMailUser = await first.CreateUserAsync();
            using var client = first.CreateClient();
            using var login = await RealCookieAccountFixture.LoginAsync(client, user.Email, user.Password);
            Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
            cookiePair = Assert.Single(login.Headers.GetValues("Set-Cookie")).Split(';', 2)[0];

            await QueuePasswordResetAsync(first, user.Id);
            await QueuePasswordResetAsync(first, staleMailUser.Id);
            await QueuePasswordResetAsync(first, expiredMailUser.Id);

            using var scope = first.Factory.Services.CreateScope();
            var protector = scope.ServiceProvider.GetRequiredService<AccountMailPayloadProtector>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            var staleUser = await users.FindByIdAsync(staleMailUser.Id)
                ?? throw new InvalidOperationException("Restored fixture account is missing.");
            Assert.True((await users.UpdateSecurityStampAsync(staleUser)).Succeeded);

            var expiredRow = await db.AccountOutbox.SingleAsync(row => row.UserId == expiredMailUser.Id);
            var expiredPayload = protector.Unprotect(expiredRow.ProtectedPayload);
            expiredRow.ProtectedPayload = protector.Protect(expiredPayload with
            {
                LinkExpiresUtc = clock.GetUtcNow().AddMinutes(-1)
            });
            await db.SaveChangesAsync();
            sourceMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
            Assert.NotEmpty(sourceMigrations);
            output.WriteLine("Applied migration revision: {0}", sourceMigrations[^1]);
        }

        // Restart the app against its original database and the same app name and wrapping certificate.
        await using (var restarted = new RealCookieAccountFixture(
            configuration,
            configureTestServices: services => services.PostConfigure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(
                options => options.ValidationInterval = TimeSpan.Zero),
            appName: appName,
            postgresConnectionString: environment.PostgresConnectionString,
            clock: clock,
            redisConnectionString: environment.RedisConnectionString,
            keyEncryptionCertificatePath: certificate.Path))
        {
            await restarted.InitializeAsync();
            using var client = restarted.CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", cookiePair);
            var session = await RealCookieAccountFixture.ReadSessionAsync(client);
            Assert.Equal(user.Id, session.User?.Id);
            Assert.Equal(sourceMigrations, await GetAppliedMigrationsAsync(restarted));

            using var scope = restarted.Factory.Services.CreateScope();
            var protector = scope.ServiceProvider.GetRequiredService<AccountMailPayloadProtector>();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await db.AccountOutbox.AsNoTracking().ToListAsync();
            Assert.Equal(3, rows.Count);
            foreach (var row in rows)
            {
                var payload = protector.Unprotect(row.ProtectedPayload);
                Assert.Equal(row.UserId, payload.UserId);
                Assert.Equal(AccountMailPurpose.PasswordReset, payload.Purpose);
                Assert.Equal("Pending", row.Status);
            }
        }

        await DumpDatabaseAsync(sourceDatabase, dump);
        await CreateDatabaseAsync(targetDatabase);
        try
        {
            await RestoreDatabaseAsync(targetDatabase, dump);
            connection.Database = targetDatabase;
            var enabledMailConfiguration = new Dictionary<string, string?>(configuration)
            {
                ["Accounts:Email:Host"] = "localhost",
                ["Accounts:Email:Port"] = "2525",
                ["Accounts:Email:From"] = "accounts@example.test",
                ["Accounts:Email:Security"] = "StartTls",
                ["Accounts:PublicOrigin"] = "https://books.example.test"
            };
            await using var restored = new RealCookieAccountFixture(
                enabledMailConfiguration,
                services =>
                {
                    services.PostConfigure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(
                        options => options.ValidationInterval = TimeSpan.Zero);
                    services.RemoveAll<IAccountMailTransport>();
                    services.AddSingleton<RestoreCapturingMailTransport>();
                    services.AddSingleton<IAccountMailTransport>(provider =>
                        provider.GetRequiredService<RestoreCapturingMailTransport>());
                    services.Configure<AccountMailWorkerSchedule>(schedule =>
                    {
                        schedule.PollInterval = TimeSpan.FromMilliseconds(20);
                        schedule.LeaseDuration = TimeSpan.FromSeconds(5);
                    });
                },
                appName: appName,
                postgresConnectionString: connection.ConnectionString,
                clock: clock,
                redisConnectionString: environment.RedisConnectionString,
                keyEncryptionCertificatePath: certificate.Path);
            await restored.InitializeAsync();

            Assert.Equal(sourceMigrations, await GetAppliedMigrationsAsync(restored));
            using var client = restored.CreateClient();
            client.DefaultRequestHeaders.Add("Cookie", cookiePair);
            var session = await RealCookieAccountFixture.ReadSessionAsync(client);
            Assert.Equal(user.Id, session.User?.Id);

            await WaitUntilAsync(async () =>
            {
                using var scope = restored.Factory.Services.CreateScope();
                var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                    .AccountOutbox.AsNoTracking().ToListAsync();
                return rows.Count == 3 && rows.All(row => row.Status is "Sent" or "Cancelled");
            });

            using (var scope = restored.Factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var rows = await db.AccountOutbox.AsNoTracking().ToListAsync();
                var delivered = Assert.Single(rows, row => row.UserId == user.Id);
                var stale = Assert.Single(rows, row => row.UserId == staleMailUser.Id);
                var expired = Assert.Single(rows, row => row.UserId == expiredMailUser.Id);
                Assert.Equal("Sent", delivered.Status);
                Assert.Null(delivered.FailureCode);
                Assert.Equal("Cancelled", stale.Status);
                Assert.Equal("AccountChanged", stale.FailureCode);
                Assert.Equal("Cancelled", expired.Status);
                Assert.Equal("Expired", expired.FailureCode);
                Assert.All(rows, row =>
                {
                    Assert.Empty(row.ProtectedPayload);
                    Assert.Null(row.LeaseId);
                });

                var sent = Assert.Single(restored.Factory.Services
                    .GetRequiredService<RestoreCapturingMailTransport>().Messages);
                Assert.Equal(user.Email, sent.Recipient);
                Assert.Equal(delivered.MessageId, sent.MessageId);

                var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
                var restoredUser = await users.FindByIdAsync(user.Id) ?? throw new InvalidOperationException("Restored fixture account is missing.");
                Assert.True((await users.UpdateSecurityStampAsync(restoredUser)).Succeeded);
            }

            restored.Clock.Advance(TimeSpan.FromSeconds(1));
            var revokedSession = await RealCookieAccountFixture.ReadSessionAsync(client);
            Assert.Null(revokedSession.User);
            var keyRows = await DataProtectionKeyStorageTests.ReadKeyRowsAsync(restored);
            Assert.NotEmpty(keyRows);
            Assert.All(keyRows, row => Assert.Contains("encryptedSecret", row.Xml, StringComparison.Ordinal));
        }
        finally
        {
            await DropDatabaseAsync(targetDatabase);
        }
    }

    private static async Task QueuePasswordResetAsync(RealCookieAccountFixture fixture, string userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId) ?? throw new InvalidOperationException("Fixture account is missing.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var outbox = scope.ServiceProvider.GetRequiredService<AccountMailOutbox>();
        var result = await outbox.RunAdmittedAsync(async (admission, cancellationToken) =>
        {
            var admittedUser = await admission.Users.FindByIdAsync(userId)
                ?? throw new InvalidOperationException("Fixture account is missing.");
            await admission.Outbox.LockUserForUpdateAsync(admittedUser, cancellationToken);
            return await admission.Outbox.EnqueueAsync(
                admittedUser,
                AccountMailPurpose.PasswordReset,
                admittedUser.Email ?? throw new InvalidOperationException("Fixture account email is missing."),
                token,
                cancellationToken: cancellationToken);
        }, AccountMailEnqueueResult.Suppressed, CancellationToken.None);
        Assert.Equal(AccountMailEnqueueResult.Queued, result);
    }

    private static async Task<string[]> GetAppliedMigrationsAsync(RealCookieAccountFixture fixture)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Database.GetAppliedMigrationsAsync()).ToArray();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        var timeout = DateTimeOffset.UtcNow.AddSeconds(15);
        while (DateTimeOffset.UtcNow < timeout)
        {
            if (await predicate()) return;
            await Task.Delay(50);
        }
        Assert.Fail("The restored account mail outbox did not reach the expected terminal state.");
    }

    private async Task ResetSharedDatabaseAsync()
    {
        await using var reset = environment.CreateFactory($"dp-restore-reset-{Guid.NewGuid():N}", runMigrationsAtStartup: true);
        await reset.InitializeAsync();
        await reset.ResetDatabaseAsync();
    }

    private async Task CreateDatabaseAsync(string databaseName)
    {
        var admin = new Npgsql.NpgsqlConnectionStringBuilder(environment.PostgresConnectionString) { Database = "postgres" };
        await using var connection = new Npgsql.NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync(string databaseName)
    {
        var admin = new Npgsql.NpgsqlConnectionStringBuilder(environment.PostgresConnectionString) { Database = "postgres" };
        await using var connection = new Npgsql.NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }

    private Task DumpDatabaseAsync(string databaseName, string path) => RunDockerPostgresToolAsync(
        ["pg_dump", "-U", PostgresUsername,
            "-d", databaseName, "-Fc"],
        inputPath: null,
        outputPath: path);

    private Task RestoreDatabaseAsync(string databaseName, string dumpPath) => RunDockerPostgresToolAsync(
        ["pg_restore", "--exit-on-error", "--no-owner", "--no-acl", "-U",
            PostgresUsername,
            "-d", databaseName],
        inputPath: dumpPath,
        outputPath: null);

    private async Task RunDockerPostgresToolAsync(string[] arguments, string? inputPath, string? outputPath)
    {
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardInput = inputPath is not null,
            RedirectStandardOutput = outputPath is not null,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.ArgumentList.Add("exec");
        if (inputPath is not null) start.ArgumentList.Add("-i");
        start.ArgumentList.Add(environment.PostgresContainerId);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start) ?? throw new InvalidOperationException("Docker process could not start.");
        Task? transfer = null;
        FileStream? destination = null;
        if (inputPath is not null)
        {
            transfer = Task.Run(async () =>
            {
                await using var source = File.OpenRead(inputPath);
                await source.CopyToAsync(process.StandardInput.BaseStream);
                process.StandardInput.Close();
            });
        }
        if (outputPath is not null)
        {
            destination = File.Create(outputPath);
            transfer = process.StandardOutput.BaseStream.CopyToAsync(destination);
        }
        var errorRead = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (transfer is not null) await transfer;
        if (destination is not null) await destination.DisposeAsync();
        var errorText = await errorRead;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Isolated PostgreSQL backup or restore failed (exit {process.ExitCode}): {errorText}");
        }
    }

    private string PostgresUsername =>
        new Npgsql.NpgsqlConnectionStringBuilder(environment.PostgresConnectionString).Username
        ?? throw new InvalidOperationException("Fixture database user is missing.");

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("GrumPlates-dp-restore-");
        public string FullName => _directory.FullName;
        public void Dispose() => _directory.Delete(recursive: true);
    }

    private sealed class RestoreCapturingMailTransport : IAccountMailTransport
    {
        private readonly ConcurrentQueue<AccountMailMessage> _messages = new();

        public IReadOnlyCollection<AccountMailMessage> Messages => _messages.ToArray();

        public Task SendAsync(AccountMailMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messages.Enqueue(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RestoreCertificate : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("GrumPlates-dp-restore-cert-");
        public string Path { get; }

        public RestoreCertificate()
        {
            Path = System.IO.Path.Combine(_directory.FullName, "restore.pfx");
            using var rsa = RSA.Create(2048);
            using var certificate = new CertificateRequest(
                    "CN=GrumPlates restored Data Protection fixture",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(5));
            File.WriteAllBytes(Path, certificate.Export(X509ContentType.Pfx, CertificatePassword));
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}


[Collection(TestCollectionNames.CrossNodeRedis)]
public sealed class DataProtectionKeyReplicaTests(SharedIntegrationEnvironment environment)
    : IClassFixture<SharedIntegrationEnvironment>
{
    private const string CertificatePassword = "fixture-key-encryption-password";


    [Fact]
    public async Task SameApplicationReplicas_ShareCookieAndPostgreSqlKeyRing()
    {
        await ResetSharedDatabaseAsync();
        var appName = $"account-replicas-{Guid.NewGuid():N}";
        using var certificate = new ReplicaCertificate();
        var configuration = new Dictionary<string, string?>
        {
            ["DataProtection:KeyEncryptionCertificatePath"] = certificate.Path,
            ["DataProtection:KeyEncryptionCertificatePassword"] = CertificatePassword
        };

        await using var first = new RealCookieAccountFixture(
            configuration,
            appName: appName,
            postgresConnectionString: environment.PostgresConnectionString,
            redisConnectionString: environment.RedisConnectionString);
        await first.InitializeAsync();
        var user = await first.CreateUserAsync();
        using var firstClient = first.CreateClient();
        using var login = await RealCookieAccountFixture.LoginAsync(firstClient, user.Email, user.Password);
        Assert.Equal(System.Net.HttpStatusCode.OK, login.StatusCode);
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"));
        var cookiePair = setCookie.Split(';', 2)[0];

        await using var second = new RealCookieAccountFixture(
            configuration,
            appName: appName,
            postgresConnectionString: environment.PostgresConnectionString,
            redisConnectionString: environment.RedisConnectionString);
        await second.InitializeAsync();
        using var secondClient = second.CreateClient();
        secondClient.DefaultRequestHeaders.Add("Cookie", cookiePair);
        var session = await RealCookieAccountFixture.ReadSessionAsync(secondClient);
        Assert.Equal(user.Id, session.User?.Id);

        var rows = await DataProtectionKeyStorageTests.ReadKeyRowsAsync(second);
        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Contains("encryptedSecret", row.Xml, StringComparison.Ordinal));
    }

    private async Task ResetSharedDatabaseAsync()
    {
        await using var reset = environment.CreateFactory(
            $"dp-reset-{Guid.NewGuid():N}",
            runMigrationsAtStartup: true);
        await reset.InitializeAsync();
        await reset.ResetDatabaseAsync();
    }

    private sealed class ReplicaCertificate : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("GrumPlates-replica-dp-cert-");
        public string Path { get; }

        public ReplicaCertificate()
        {
            Path = System.IO.Path.Combine(_directory.FullName, "fixture.pfx");
            using var rsa = RSA.Create(2048);
            using var certificate = new CertificateRequest(
                    "CN=GrumPlates replica data protection fixture",
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1)
                .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddYears(5));
            File.WriteAllBytes(Path, certificate.Export(X509ContentType.Pfx, CertificatePassword));
        }

        public void Dispose() => _directory.Delete(recursive: true);
    }
}
