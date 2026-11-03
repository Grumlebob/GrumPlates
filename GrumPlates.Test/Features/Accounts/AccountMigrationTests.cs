using GrumPlates.Infrastructure.Persistence;
using GrumPlates.Test.TestSupport.Integration;
using Microsoft.AspNetCore.Identity;
using Testcontainers.PostgreSql;

namespace GrumPlates.Test.Features.Accounts;

public sealed class AccountMigrationTests
{
    private static readonly ServiceProvider IdentitySchemaVersionServices = new ServiceCollection()
        .Configure<IdentityOptions>(options => options.Stores.SchemaVersion = IdentitySchemaVersions.Version3)
        .BuildServiceProvider();

    [Fact]
    public async Task Migrate_CreatesUniqueEmailOutboxAndDataProtectionKeyStorage()
    {
        await using var container = await StartDatabaseAsync();
        await using var db = CreateDb(container);

        await db.Database.MigrateAsync();
        var indexDefinition = await ScalarAsync<string>(db,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = current_schema() AND indexname = 'EmailIndex'");
        Assert.Contains("UNIQUE", indexDefinition, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE (\"NormalizedEmail\" IS NOT NULL)", indexDefinition, StringComparison.OrdinalIgnoreCase);

        Assert.True(await TableExistsAsync(db, "AccountOutbox"));
        Assert.True(await TableExistsAsync(db, "DataProtectionKeys"));
        Assert.True(await ColumnExistsAsync(db, "AccountOutbox", "ProtectedPayload"));
        Assert.False(await ColumnExistsAsync(db, "AccountOutbox", "Recipient"));
        Assert.False(await ColumnExistsAsync(db, "AccountOutbox", "Token"));
        Assert.False(await ColumnExistsAsync(db, "AccountOutbox", "Body"));
    }

    private static async Task<PostgreSqlContainer> StartDatabaseAsync()
    {
        var container = TestContainerImages.CreatePostgreSqlFixture()
            .WithLabel(TestContainerLabels.For("account-migration-tests"))
            .WithCleanUp(true)
            .Build();
        await container.StartAsync();
        return container;
    }

    private static AppDbContext CreateDb(PostgreSqlContainer container) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseApplicationServiceProvider(IdentitySchemaVersionServices)
            .UseNpgsql(container.GetConnectionString())
            .Options);

    private static async Task<bool> TableExistsAsync(AppDbContext db, string table) =>
        await ScalarAsync<bool>(db,
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = current_schema() AND table_name = @table)",
            ("table", table));

    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column) =>
        await ScalarAsync<bool>(db,
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @table AND column_name = @column)",
            ("table", table), ("column", column));

    private static async Task<T> ScalarAsync<T>(AppDbContext db, string sql, params (string Name, object Value)[] parameters)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
        return (T)(await command.ExecuteScalarAsync())!;
    }
}
