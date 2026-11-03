using GrumPlates.Test.Architecture.Support;
using Microsoft.AspNetCore.Builder;

namespace GrumPlates.Test.Infrastructure.Hosting;

public sealed class AppSettingsEnvironmentTests
{
    [Fact]
    public void DockerEnvironment_ReadsDockerSettings()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Docker",
            ContentRootPath = Path.Combine(SourceSearch.GetRepoRoot(), "GrumPlates")
        });

        Assert.Equal("http://alloy:4317", builder.Configuration["Observability:OpenTelemetry:Endpoint"]);
    }
}
