using System.Security.Claims;
using GrumPlates.Core.Features.Books.Contracts;
using GrumPlates.Core.Features.Books.UseCases.GetBooks;
using Microsoft.AspNetCore.Components.Authorization;

namespace GrumPlates.Client.Features.Books.UserBookcase;

public sealed class UserBookcaseState(
    IBooksApi books,
    AuthenticationStateProvider authenticationStateProvider,
    ILogger<UserBookcaseState> logger)
{
    private readonly IBooksApi _books = books;
    private readonly AuthenticationStateProvider _authenticationStateProvider = authenticationStateProvider;
    private readonly ILogger<UserBookcaseState> _logger = logger;
    private int _version;

    public event Action? Changed;

    public bool IsLoading { get; private set; } = true;

    public string? Error { get; private set; }

    private string? _currentUserKey;

    public IReadOnlyList<BookListItemResponse> Books { get; private set; } = [];

    public int Page { get; private set; } = 1;

    public int PageSize { get; private set; } = SearchBooksRequest.DefaultPageSize;

    public int TotalCount { get; private set; }

    public string SearchTerm { get; private set; } = string.Empty;

    public bool HasPreviousPage => Page > 1;

    public bool HasNextPage => Page * PageSize < TotalCount;

    public Task LoadForCurrentUserAsync(CancellationToken cancellationToken = default) =>
        LoadPageForCurrentUserAsync(null, null, cancellationToken);

    public Task SearchAsync(string? searchTerm, CancellationToken cancellationToken = default) =>
        LoadPageForCurrentUserAsync(searchTerm ?? string.Empty, 1, cancellationToken);

    public Task GoToPageAsync(int page, CancellationToken cancellationToken = default) =>
        LoadPageForCurrentUserAsync(SearchTerm, page, cancellationToken);

    public void ResetForIdentity(string? userId)
    {
        if (string.Equals(userId, _currentUserKey, StringComparison.Ordinal))
        {
            return;
        }

        _currentUserKey = userId;
        _version++;
        Books = [];
        Page = 1;
        TotalCount = 0;
        SearchTerm = string.Empty;
        Error = null;
        IsLoading = false;
        NotifyChanged();
    }

    public void ResetForIdentity(ClaimsPrincipal user) => ResetForIdentity(BookUserIdentity.GetUserKey(user));

    private async Task LoadPageForCurrentUserAsync(
        string? searchTerm,
        int? page,
        CancellationToken cancellationToken)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var userId = BookUserIdentity.GetUserKey(authState.User);
        var identityChanged = !string.Equals(userId, _currentUserKey, StringComparison.Ordinal);
        if (identityChanged)
        {
            ResetForIdentity(userId);
        }

        SearchTerm = searchTerm ?? (identityChanged ? string.Empty : SearchTerm);
        Page = Math.Clamp(page ?? (identityChanged ? 1 : Page), 1, SearchBooksRequest.MaximumPage);
        var version = ++_version;
        IsLoading = true;
        Error = null;
        NotifyChanged();

        if (string.IsNullOrWhiteSpace(userId))
        {
            Books = [];
            IsLoading = false;
            ApplyLoadResult(version, new SearchBooksResponse
            {
                Books = [],
                Page = Page,
                PageSize = PageSize,
                TotalCount = 0
            });
            return;
        }

        try
        {
            var response = await _books.SearchAsync(new SearchBooksRequest
            {
                SearchTerm = SearchTerm,
                Page = Page,
                PageSize = PageSize
            }, cancellationToken);
            if (version != _version)
            {
                return;
            }

            var latestAuthState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var latestUserId = BookUserIdentity.GetUserKey(latestAuthState.User);
            if (!string.Equals(userId, latestUserId, StringComparison.Ordinal))
            {
                ResetForIdentity(latestUserId);
                return;
            }

            ApplyLoadResult(version, response);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Component disposal cancels this load; it is not a user-visible failure.
        }
        catch (Exception ex)
        {
            if (version != _version)
            {
                return;
            }

            _logger.LogWarning(ex, "User books load failed for user {UserId}", userId);
            Error = "Your books could not be loaded.";
            IsLoading = false;
            NotifyChanged();
        }
    }

    private void ApplyLoadResult(int version, SearchBooksResponse response)
    {
        if (version != _version)
        {
            return;
        }

        Books = response.Books.OrderBy(book => book.Id).ToList();
        Page = response.Page;
        PageSize = response.PageSize;
        TotalCount = response.TotalCount;
        Error = null;
        IsLoading = false;
        NotifyChanged();
    }

    private void NotifyChanged() => Changed?.Invoke();
}
