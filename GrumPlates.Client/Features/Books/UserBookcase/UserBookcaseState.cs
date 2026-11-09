using System.Security.Claims;
using GrumPlates.Core.Features.Books.Contracts;
using GrumPlates.Core.Features.Books.UseCases.DeleteBook;
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

    public async Task LoadForCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        await LoadPageForCurrentUserAsync(null, null, cancellationToken);
    }

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
        Interlocked.Increment(ref _version);
        ApplyBooks([]);
        Page = 1;
        TotalCount = 0;
        SearchTerm = string.Empty;
        Error = null;
        IsLoading = false;
        NotifyChanged();
    }

    public void ResetForIdentity(ClaimsPrincipal user) => ResetForIdentity(GetUserKey(user));

    private async Task LoadPageForCurrentUserAsync(
        string? searchTerm,
        int? page,
        CancellationToken cancellationToken)
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var userId = GetUserKey(authState.User);
        var identityChanged = !string.Equals(userId, _currentUserKey, StringComparison.Ordinal);
        if (identityChanged)
        {
            ResetForIdentity(userId);
        }

        SearchTerm = searchTerm ?? (identityChanged ? string.Empty : SearchTerm);
        Page = Math.Clamp(page ?? (identityChanged ? 1 : Page), 1, SearchBooksRequest.MaximumPage);
        var version = Interlocked.Increment(ref _version);
        IsLoading = true;
        Error = null;
        NotifyChanged();

        if (string.IsNullOrWhiteSpace(userId))
        {
            ApplyBooks([]);
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
            if (version != Volatile.Read(ref _version))
            {
                return;
            }

            var latestAuthState = await _authenticationStateProvider.GetAuthenticationStateAsync();
            var latestUserId = GetUserKey(latestAuthState.User);
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
            if (version != Volatile.Read(ref _version))
            {
                return;
            }

            _logger.LogWarning(ex, "User books load failed for user {UserId}", userId);
            Error = "Your books could not be loaded.";
            IsLoading = false;
            NotifyChanged();
        }
    }

    public void ApplySavedBook(BookListItemResponse savedBook)
    {
        Interlocked.Increment(ref _version);
        var isNewBook = Books.All(book => book.Id != savedBook.Id);
        var next = Books
            .Where(book => book.Id != savedBook.Id)
            .Append(savedBook)
            .OrderBy(book => book.Id)
            .ToList();

        ApplyBooks(next);
        if (isNewBook)
        {
            TotalCount++;
        }

        IsLoading = false;
        Error = null;
        NotifyChanged();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var version = Interlocked.Increment(ref _version);
        try
        {
            var success = await _books.DeleteAsync(new DeleteBookRequest { Id = id }, cancellationToken);
            if (!success)
            {
                return false;
            }

            if (version == Volatile.Read(ref _version))
            {
                var wasLoaded = Books.Any(book => book.Id == id);
                ApplyBooks(Books.Where(book => book.Id != id).ToList());
                if (wasLoaded)
                {
                    TotalCount = Math.Max(0, TotalCount - 1);
                }

                IsLoading = false;
                Error = null;
                NotifyChanged();
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Navigation or component disposal superseded this delete.
            return false;
        }
        catch (Exception ex)
        {
            if (version == Volatile.Read(ref _version))
            {
                _logger.LogWarning(ex, "User book delete failed for book {BookId}", id);
                Error = "Book could not be deleted.";
                IsLoading = false;
                NotifyChanged();
            }

            return false;
        }
    }

    private void ApplyLoadResult(int version, SearchBooksResponse response)
    {
        if (version != Volatile.Read(ref _version))
        {
            return;
        }

        ApplyBooks(response.Books.OrderBy(book => book.Id).ToList());
        Page = response.Page;
        PageSize = response.PageSize;
        TotalCount = response.TotalCount;
        Error = null;
        IsLoading = false;
        NotifyChanged();
    }

    private void ApplyBooks(IReadOnlyList<BookListItemResponse> books)
    {
        Books = books;
    }

    private void NotifyChanged() => Changed?.Invoke();

    private static string? GetUserKey(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
               user.FindFirst(ClaimTypes.Name)?.Value ??
               user.Identity.Name;
    }
}
