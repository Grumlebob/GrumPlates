using System.Security.Claims;
using GrumPlates.Features.Accounts.Blazor;
using Microsoft.AspNetCore.Components.Authorization;

namespace GrumPlates.Test.Features.Accounts;

public sealed class AccountAuthenticationStateSerializationTests
{
    [Fact]
    public async Task Serialize_SendsNameRolesAndUserIdButNotOtherClaims()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-id"),
                new Claim(ClaimTypes.Name, "user@example.test"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("AspNet.Identity.SecurityStamp", "stamp"),
            ],
            "Identity.Application");

        var data = await AccountAuthenticationStateSerialization.Serialize(new AuthenticationState(new ClaimsPrincipal(identity)));

        Assert.NotNull(data);
        Assert.Equal(
            [(ClaimTypes.NameIdentifier, "user-id"), (ClaimTypes.Name, "user@example.test"), (ClaimTypes.Role, "Admin")],
            data.Claims.Select(claim => (claim.Type, claim.Value)));
    }

    [Fact]
    public async Task Serialize_ReturnsNothingForAnonymousUsers()
    {
        var data = await AccountAuthenticationStateSerialization.Serialize(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

        Assert.Null(data);
    }
}
