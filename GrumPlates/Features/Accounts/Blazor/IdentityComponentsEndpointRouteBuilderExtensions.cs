using GrumPlates.Core.Features.Accounts.Contracts;
using GrumPlates.Features.Accounts.Blazor.Pages;
using GrumPlates.Features.Accounts.Blazor.Pages.Manage;
using GrumPlates.Infrastructure.Hosting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using ApplicationUser = GrumPlates.Features.Login.Account.ApplicationUser;

namespace GrumPlates.Features.Accounts.Blazor;

internal static class IdentityComponentsEndpointRouteBuilderExtensions
{
    // These endpoints are required by the Identity Razor components under Features/Accounts/Blazor/Pages.
    public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var accountGroup = endpoints.MapGroup("/Account");
        accountGroup.RequireRateLimiting(AppRateLimiting.AccountWritePolicyName);

        accountGroup.MapPost("/PerformExternalLogin", (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider,
            [FromForm] string returnUrl) =>
        {
            IEnumerable<KeyValuePair<string, StringValues>> query = [
                new("ReturnUrl", returnUrl),
                new("Action", ExternalLogin.LoginCallbackAction)];

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/ExternalLogin",
                QueryString.Create(query));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return TypedResults.Challenge(properties, [provider]);
        });

        accountGroup.MapPost("/Logout", async (
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string? returnUrl) =>
        {
            await signInManager.SignOutAsync();
            var localReturnUrl = string.IsNullOrWhiteSpace(returnUrl)
                ? "/"
                : $"/{returnUrl.TrimStart('/')}";
            return TypedResults.LocalRedirect($"~{localReturnUrl}");
        })
        .RequireAuthorization();

        accountGroup.MapPost("/PasskeyCreationOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return AccountProblem.Create(400, "CsrfInvalid", "The request could not be verified");
            }

            var user = await userManager.GetUserAsync(context.User);
            if (user is null)
            {
                return Results.NotFound("Unable to load the current user.");
            }

            var userId = await userManager.GetUserIdAsync(user);
            var userName = await userManager.GetUserNameAsync(user) ?? "User";
            var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
            {
                Id = userId,
                Name = userName,
                DisplayName = userName
            });
            return TypedResults.Content(optionsJson, contentType: "application/json");
        });

        accountGroup.MapPost("/PasskeyRequestOptions", async (
            HttpContext context,
            [FromServices] UserManager<ApplicationUser> userManager,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromServices] IAntiforgery antiforgery,
            [FromQuery] string? username) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return AccountProblem.Create(400, "CsrfInvalid", "The request could not be verified");
            }

            var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
            var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
            return TypedResults.Content(optionsJson, contentType: "application/json");
        });

        var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

        manageGroup.MapPost("/LinkExternalLogin", async (
            HttpContext context,
            [FromServices] SignInManager<ApplicationUser> signInManager,
            [FromForm] string provider) =>
        {
            // Clear the existing external cookie to ensure a clean login process
            await context.SignOutAsync(IdentityConstants.ExternalScheme);

            var redirectUrl = UriHelper.BuildRelative(
                context.Request.PathBase,
                "/Account/Manage/ExternalLogins",
                QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

            var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
            return TypedResults.Challenge(properties, [provider]);
        });

        manageGroup.MapPost("/DownloadPersonalData", async (
            HttpContext context,
            [FromForm] AccountPasswordRequest request,
            [FromServices] AccountOperations accounts) =>
        {
            var operation = await accounts.ExportAsync(request);
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode == StatusCodes.Status200OK)
                {
                    context.Response.Headers.ContentDisposition = "attachment; filename=AccountExport.json";
                }
                return Task.CompletedTask;
            });
            await operation.ExecuteAsync(context);
            return TypedResults.Empty;
        });

        return accountGroup;
    }
}
