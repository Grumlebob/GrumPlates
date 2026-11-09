using System.Security.Claims;
using GrumPlates.Client.Features.Books.UserBookcase;
using GrumPlates.Core.Features.Books.Contracts;
using GrumPlates.Core.Features.Books.UseCases.CreateBook;
using GrumPlates.Core.Features.Books.UseCases.DeleteBook;
using GrumPlates.Core.Features.Books.UseCases.GetBook;
using GrumPlates.Core.Features.Books.UseCases.GetBooks;
using GrumPlates.Core.Features.Books.UseCases.UpdateBook;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrumPlates.Test.Features.Books.Client;

public sealed class UserBookcaseStateTests
{
    [Fact]
    public async Task LoadForCurrentUserAsync_IgnoresOlderResponseAfterNewerLoad()
    {
        var olderResponse = new TaskCompletionSource<SearchBooksResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var newerResponse = new TaskCompletionSource<SearchBooksResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requestCount = 0;
        var api = new StubBooksApi
        {
            Search = (_, _) => Interlocked.Increment(ref requestCount) switch
            {
                1 => olderResponse.Task,
                2 => newerResponse.Task,
                _ => throw new InvalidOperationException("Unexpected bookcase request.")
            }
        };
        var state = CreateState(api);

        var olderLoad = state.LoadForCurrentUserAsync();
        var newerLoad = state.LoadForCurrentUserAsync();
        newerResponse.SetResult(CreateResponse(CreateBook(2, "Newer")));
        await newerLoad;
        olderResponse.SetResult(CreateResponse(CreateBook(1, "Older")));
        await olderLoad;

        var book = Assert.Single(state.Books);
        Assert.Equal(2, book.Id);
        Assert.Equal("Newer", book.Title);
        Assert.Null(state.Error);
        Assert.False(state.IsLoading);
    }

    [Fact]
    public async Task LoadForCurrentUserAsync_ClearsPreviousOwnersRowsBeforeLoadingAnotherIdentity()
    {
        var authentication = new TestAuthenticationStateProvider("user-1");
        var userTwoResponse = new TaskCompletionSource<SearchBooksResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        var api = new StubBooksApi
        {
            Search = (_, _) => Task.FromResult(CreateResponse(CreateBook(4, "User one")))
        };
        var state = new UserBookcaseState(api, authentication, NullLogger<UserBookcaseState>.Instance);
        await state.LoadForCurrentUserAsync();
        Assert.Equal("User one", Assert.Single(state.Books).Title);

        authentication.SetUser("user-2");
        api.Search = (_, _) => userTwoResponse.Task;
        var userTwoLoad = state.LoadForCurrentUserAsync();

        Assert.Empty(state.Books);
        userTwoResponse.SetResult(CreateResponse(CreateBook(9, "User two")));
        await userTwoLoad;

        Assert.Equal("User two", Assert.Single(state.Books).Title);
    }

    [Fact]
    public async Task SearchAsync_UsesPaginatedContractAndExposesSearchState()
    {
        var requests = new List<SearchBooksRequest>();
        var api = new StubBooksApi
        {
            Search = (request, _) =>
            {
                requests.Add(request);
                return Task.FromResult(new SearchBooksResponse
                {
                    Books = [CreateBook(12, "Matching book")],
                    Page = request.EffectivePage,
                    PageSize = request.EffectivePageSize,
                    TotalCount = 51
                });
            }
        };
        var state = CreateState(api);

        await state.SearchAsync("  Matching  ");
        await state.GoToPageAsync(2);

        Assert.Collection(requests,
            request =>
            {
                Assert.Equal("  Matching  ", request.SearchTerm);
                Assert.Equal(1, request.EffectivePage);
                Assert.Equal(SearchBooksRequest.DefaultPageSize, request.EffectivePageSize);
            },
            request =>
            {
                Assert.Equal("  Matching  ", request.SearchTerm);
                Assert.Equal(2, request.EffectivePage);
            });
        Assert.Equal("  Matching  ", state.SearchTerm);
        Assert.Equal(2, state.Page);
        Assert.Equal(51, state.TotalCount);
        Assert.True(state.HasPreviousPage);
        Assert.True(state.HasNextPage);
        Assert.Equal("Matching book", Assert.Single(state.Books).Title);
    }

    [Fact]
    public async Task LoadForCurrentUserAsync_ClearsSearchAndPageAfterIdentityChanges()
    {
        var authentication = new TestAuthenticationStateProvider("user-1");
        var api = new StubBooksApi
        {
            Search = (request, _) => Task.FromResult(new SearchBooksResponse
            {
                Books = [CreateBook(request.EffectivePage, "Book")],
                Page = request.EffectivePage,
                PageSize = request.EffectivePageSize,
                TotalCount = 100
            })
        };
        var state = new UserBookcaseState(api, authentication, NullLogger<UserBookcaseState>.Instance);
        await state.SearchAsync("prior user's search");
        await state.GoToPageAsync(3);

        authentication.SetUser("user-2");
        await state.LoadForCurrentUserAsync();

        Assert.Equal(string.Empty, state.SearchTerm);
        Assert.Equal(1, state.Page);
        Assert.Equal(100, state.TotalCount);
        Assert.Equal(1, Assert.Single(state.Books).Id);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalseAndPreservesBookWhenCanceled()
    {
        var api = new StubBooksApi
        {
            Delete = (_, cancellationToken) => Task.FromCanceled<bool>(cancellationToken)
        };
        var state = CreateState(api);
        state.ApplySavedBook(CreateBook(4, "Keep me"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var deleted = await state.DeleteAsync(4, cancellation.Token);

        Assert.False(deleted);
        Assert.Equal(new[] { 4 }, state.Books.Select(book => book.Id));
    }

    [Fact]
    public async Task DeleteAsync_UsesRequestedIdAndKeepsOtherBooks()
    {
        int? requestedId = null;
        var api = new StubBooksApi
        {
            Delete = (request, _) =>
            {
                requestedId = request.Id;
                return Task.FromResult(true);
            }
        };
        var state = CreateState(api);
        state.ApplySavedBook(CreateBook(4, "First"));
        state.ApplySavedBook(CreateBook(9, "Second"));

        var deleted = await state.DeleteAsync(4);

        Assert.True(deleted);
        Assert.Equal(4, requestedId);
        Assert.Equal(new[] { 9 }, state.Books.Select(book => book.Id));
        Assert.Equal(1, state.TotalCount);
    }

    private static UserBookcaseState CreateState(StubBooksApi api) => new(
        api,
        new TestAuthenticationStateProvider("user-1"),
        NullLogger<UserBookcaseState>.Instance);

    private static SearchBooksResponse CreateResponse(params BookListItemResponse[] books) => new()
    {
        Books = [.. books],
        Page = 1,
        PageSize = SearchBooksRequest.DefaultPageSize,
        TotalCount = books.Length
    };

    private static BookListItemResponse CreateBook(int id, string title) => new()
    {
        Id = id,
        Title = title
    };

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _authenticationState = new(new ClaimsPrincipal(new ClaimsIdentity()));

        public TestAuthenticationStateProvider(string userId) => SetUser(userId, notify: false);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(_authenticationState);

        public void SetUser(string? userId, bool notify = true)
        {
            var identity = userId is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], authenticationType: "test");
            _authenticationState = new AuthenticationState(new ClaimsPrincipal(identity));
            if (notify)
            {
                NotifyAuthenticationStateChanged(Task.FromResult(_authenticationState));
            }
        }
    }

    private sealed class StubBooksApi : IBooksApi
    {
        public Func<CancellationToken, Task<GetBooksResponse>> Get { get; init; } =
            _ => Task.FromResult(new GetBooksResponse { Books = [] });

        public Func<SearchBooksRequest, CancellationToken, Task<SearchBooksResponse>> Search { get; set; } =
            (_, _) => Task.FromResult(CreateResponse());

        public Func<DeleteBookRequest, CancellationToken, Task<bool>> Delete { get; init; } =
            (_, _) => Task.FromResult(false);

        public Task<GetBooksResponse> GetAsync(CancellationToken cancellationToken = default) =>
            Get(cancellationToken);

        public Task<SearchBooksResponse> SearchAsync(SearchBooksRequest req, CancellationToken cancellationToken = default) =>
            Search(req, cancellationToken);

        public Task<GetBookResponse?> GetByIdAsync(GetBookRequest req, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CreateBookResponse> CreateAsync(CreateBookRequest req, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> UpdateAsync(UpdateBookRequest req, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(DeleteBookRequest req, CancellationToken cancellationToken = default) =>
            Delete(req, cancellationToken);
    }
}
