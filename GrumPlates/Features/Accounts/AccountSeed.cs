using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using ApplicationUser = GrumPlates.Features.Login.Account.ApplicationUser;

namespace GrumPlates.Features.Accounts;

// Creates each configured account once. Existing accounts, passwords and emails are never changed.
internal static class AccountSeed
{
    public static Task SeedConfiguredAccountsAsync(this WebApplication app) =>
        SeedConfiguredAccountsAsync(
            app.Services.GetRequiredService<IOptions<AccountOptions>>().Value,
            app.Services,
            app.Logger);

    internal static async Task SeedConfiguredAccountsAsync(
        AccountOptions options,
        IServiceProvider services,
        ILogger logger)
    {
        (AccountSeedOptions Seed, string Role)[] seeds = [(options.AdminSeed, "Admin"), (options.UserSeed, "User")];
        if (!seeds.Any(entry => entry.Seed.Enabled))
        {
            return;
        }

        using var strategyScope = services.CreateScope();
        var strategy = strategyScope.ServiceProvider.GetRequiredService<AppDbContext>().Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            await using var transaction = await db.Database.BeginTransactionAsync();
            // One transaction-scoped database lock coordinates the whole seeding run across replicas.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(73529301015421)");
            foreach (var (seed, role) in seeds.Where(entry => entry.Seed.Enabled))
            {
                await SeedAsync(seed, role, users, roles, logger);
            }
            await transaction.CommitAsync();
        });
    }

    private static async Task SeedAsync(
        AccountSeedOptions seed,
        string role,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        ILogger logger)
    {
        var existing = await users.FindByEmailAsync(seed.Email);
        if (existing is not null)
        {
            if (!await users.IsInRoleAsync(existing, role))
            {
                throw new InvalidOperationException($"{role} seed refused: the configured email already belongs to an account without the {role} role.");
            }

            logger.LogInformation("Configured {Role} account already exists; no account changes made.", role);
            return;
        }

        if (!await roles.RoleExistsAsync(role))
        {
            Ensure(await roles.CreateAsync(new IdentityRole(role)), role);
        }
        var user = new ApplicationUser
        {
            UserName = seed.Email,
            Email = seed.Email,
            EmailConfirmed = true
        };
        Ensure(await users.CreateAsync(user, seed.Password), role);
        Ensure(await users.AddToRoleAsync(user, role), role);
        logger.LogInformation("Configured {Role} account provisioned.", role);
    }

    private static void Ensure(IdentityResult result, string role)
    {
        if (!result.Succeeded)
        {
            // Codes identify the failing rule without including any configured credential.
            throw new InvalidOperationException($"{role} seed failed: {string.Join(", ", result.Errors.Select(error => error.Code))}.");
        }
    }
}
