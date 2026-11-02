using System.Collections.Concurrent;
using GrumPlates.Core.Features.Accounts.Contracts;

namespace GrumPlates.Test.TestSupport.Integration;

/// <summary>
/// Sends the account headers that the real frontends send for the fake test principal:
/// the expected user on every request and the antiforgery token on unsafe requests.
/// Headers a test sets itself are left untouched.
/// </summary>
internal sealed class TestAccountHeadersHandler : DelegatingHandler
{
    public const string ExpectedUserHeader = "X-Expected-User";
    public const string CsrfHeader = "X-CSRF-TOKEN";
    private readonly ConcurrentDictionary<string, string> _csrfTokens = new(StringComparer.Ordinal);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.TryGetValues(TestAuthenticationHandler.UserHeader, out var users))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var user = users.First();
        if (!request.Headers.Contains(ExpectedUserHeader))
        {
            request.Headers.Add(ExpectedUserHeader, user);
        }

        var unsafeMethod = request.Method != HttpMethod.Get && request.Method != HttpMethod.Head && request.Method != HttpMethod.Options;
        if (unsafeMethod && !request.Headers.Contains(CsrfHeader))
        {
            if (!_csrfTokens.TryGetValue(user, out var token))
            {
                token = await ReadCsrfTokenAsync(request.RequestUri!, user, cancellationToken);
                _csrfTokens[user] = token;
            }
            request.Headers.Add(CsrfHeader, token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

    private async Task<string> ReadCsrfTokenAsync(Uri requestUri, string user, CancellationToken cancellationToken)
    {
        using var sessionRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(requestUri, "/api/auth/session"));
        sessionRequest.Headers.Add(TestAuthenticationHandler.UserHeader, user);
        using var response = await base.SendAsync(sessionRequest, cancellationToken);
        response.EnsureSuccessStatusCode();
        var session = await response.Content.ReadFromJsonAsync<AccountSession>(cancellationToken)
            ?? throw new InvalidOperationException("Test session response was missing.");
        return session.CsrfToken ?? throw new InvalidOperationException("Test session did not include an antiforgery token.");
    }
}
