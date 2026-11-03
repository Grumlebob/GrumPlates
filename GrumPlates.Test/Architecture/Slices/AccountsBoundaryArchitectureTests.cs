using GrumPlates.Test.Architecture.Support;

namespace GrumPlates.Test.Architecture.Slices;

public class AccountsBoundaryArchitectureTests
{
    [Fact]
    public void ProductionAssemblies_HaveNoTypesInTheRetiredLoginNamespaces()
    {
        var retiredPrefixes = new[]
        {
            "GrumPlates.Core.Features.Login",
            "GrumPlates.Features.Login",
            "GrumPlates.Client.Features.Login"
        };
        var offenders = new[]
        {
            ArchitectureAssemblies.Core,
            ArchitectureAssemblies.Server,
            ArchitectureAssemblies.Client
        }.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Namespace is { } typeNamespace && retiredPrefixes.Any(prefix =>
                typeNamespace == prefix || typeNamespace.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .Select(type => type.FullName)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Account types belong under the Accounts feature namespaces:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void ProductionSource_DoesNotUseTheRetiredLoginFeatureFolder()
    {
        var root = SourceSearch.GetRepoRoot();
        var projectFolders = new[] { "GrumPlates.Core", "GrumPlates", "GrumPlates.Client" };
        var offenders = projectFolders
            .Select(project => Path.Combine(root, project, "Features", "Login"))
            .Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            .Where(file => Path.GetExtension(file) is ".cs" or ".razor")
            .Select(file => Path.GetRelativePath(root, file))
            .OrderBy(file => file)
            .ToArray();

        Assert.True(offenders.Length == 0,
            "Account source belongs under Features/Accounts:\n" +
            string.Join("\n", offenders));
        Assert.True(File.Exists(Path.Combine(root, "GrumPlates", "Features", "Accounts", "Identity", "ApplicationUser.cs")),
            "The mapped Identity user source belongs to Features/Accounts/Identity.");
    }
}
