namespace GrumPlates.Test.E2E.Support;

/// <summary>
/// The local seeded User that Blazor E2E signs in as. Never log or display the password.
/// </summary>
internal static class E2ESeededUser
{
    private const string EmailKey = "Accounts__UserSeed__Email";
    private const string PasswordKey = "Accounts__UserSeed__Password";

    public static (string Email, string Password) Resolve()
    {
        var email = Environment.GetEnvironmentVariable("E2E_USER_EMAIL");
        var password = Environment.GetEnvironmentVariable("E2E_USER_PASSWORD");
        if (!string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(password))
        {
            return (email, password);
        }

        var env = ReadRepositoryDotEnv();
        if (env.TryGetValue(EmailKey, out email) && !string.IsNullOrEmpty(email) &&
            env.TryGetValue(PasswordKey, out password) && !string.IsNullOrEmpty(password))
        {
            return (email, password);
        }

        throw new InvalidOperationException(
            "Blazor E2E needs the seeded User. Run Scripts/LocalDevelopment/setup-local.ps1, or set E2E_USER_EMAIL and E2E_USER_PASSWORD.");
    }

    // Raw KEY=value lines, as Compose env_file reads them: no trimming or unquoting of values.
    private static Dictionary<string, string> ReadRepositoryDotEnv()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return values;
        }

        var envPath = Path.Combine(root, ".env");
        if (!File.Exists(envPath))
        {
            return values;
        }

        foreach (var line in File.ReadLines(envPath))
        {
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=', StringComparison.Ordinal);
            if (separator > 0)
            {
                values[line[..separator]] = line[(separator + 1)..];
            }
        }

        return values;
    }

    private static string? FindRepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "GrumPlates.sln")))
            {
                return current.FullName;
            }
        }

        return null;
    }
}
