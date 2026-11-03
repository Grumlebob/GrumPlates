using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace GrumPlates.Features.Accounts.Blazor;

/// <summary>
/// Sends WebAssembly the name, roles and user ID. The Books client needs the ID for its
/// X-Expected-User header. Other claims, such as the security stamp, stay on the server.
/// </summary>
internal static class AccountAuthenticationStateSerialization
{
    public static ValueTask<AuthenticationStateData?> Serialize(AuthenticationState authenticationState)
    {
        var user = authenticationState.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return ValueTask.FromResult<AuthenticationStateData?>(null);
        }

        var data = new AuthenticationStateData();
        if (user.Identities.FirstOrDefault() is { } identity)
        {
            data.NameClaimType = identity.NameClaimType;
            data.RoleClaimType = identity.RoleClaimType;
        }

        foreach (var claim in user.Claims)
        {
            if (claim.Type == data.NameClaimType || claim.Type == data.RoleClaimType || claim.Type == ClaimTypes.NameIdentifier)
            {
                data.Claims.Add(new ClaimData(claim.Type, claim.Value));
            }
        }

        return ValueTask.FromResult<AuthenticationStateData?>(data);
    }
}
