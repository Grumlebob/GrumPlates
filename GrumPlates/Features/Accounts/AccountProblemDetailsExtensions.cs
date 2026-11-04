namespace GrumPlates.Features.Accounts;

internal static class AccountProblemDetailsExtensions
{
    // Gives every /api/auth problem response a stable account error code.
    public static IServiceCollection AddAccountProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                if (!context.HttpContext.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
                    || context.ProblemDetails.Extensions.ContainsKey("code"))
                {
                    return;
                }

                context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
                {
                    StatusCodes.Status400BadRequest => "RequestInvalid",
                    StatusCodes.Status401Unauthorized => "AuthenticationRequired",
                    StatusCodes.Status403Forbidden => "CapabilityDisabled",
                    StatusCodes.Status409Conflict => "AccountConflict",
                    StatusCodes.Status413PayloadTooLarge => "RequestTooLarge",
                    StatusCodes.Status415UnsupportedMediaType => "UnsupportedMediaType",
                    StatusCodes.Status429TooManyRequests => "RateLimited",
                    StatusCodes.Status503ServiceUnavailable => "AccountServiceUnavailable",
                    _ => "AccountRequestFailed"
                };
            };
        });
}
