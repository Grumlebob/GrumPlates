using System.Security.Claims;
using GrumPlates.Client.Features.Books.Routes;
using GrumPlates.Client.Features.Books.UserBookcase;
using GrumPlates.Core.Features.Books.Contracts;
using GrumPlates.Core.Features.Books.UseCases.CreateBook;
using GrumPlates.Core.Features.Books.UseCases.DeleteBook;
using GrumPlates.Core.Features.Books.UseCases.GetBook;
using GrumPlates.Core.Features.Books.UseCases.GetBooks;
using GrumPlates.Core.Features.Books.UseCases.UpdateBook;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrumPlates.Test.Features.Books.Client;

public sealed class BookEditAuthenticationTests
{
    [Fact]
    public async Task ChangedIdentity_CancelsPendingLoadAndDoesNotRenderPreviousOwnersBook()
    {
        var books = new ControlledBooksApi();
        var authentication = new MutableAuthenticationStateProvider("first-user");
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IBooksApi>(books)
            .AddSingleton<AuthenticationStateProvider>(authentication)
            .AddSingleton<NavigationManager>(new TestNavigationManager())
            .AddSingleton(new UserBookcaseState(books, authentication, NullLogger<UserBookcaseState>.Instance))
            .BuildServiceProvider();

        await using var serviceProvider = services;
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var rendered = await renderer.Dispatcher.InvokeAsync(() =>
            renderer.BeginRenderingComponent<BookEdit>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(BookEdit.Id)] = 7 })));
        await books.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            await renderer.Dispatcher.InvokeAsync(() => authentication.ChangeTo("second-user"));
            await books.SecondReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(books.FirstReadToken.IsCancellationRequested);

            books.FirstRead.TrySetResult(new GetBookResponse { Id = 7, Title = "First account private title" });
            await rendered.QuiescenceTask.WaitAsync(TimeSpan.FromSeconds(10));

            var html = string.Empty;
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (DateTimeOffset.UtcNow < deadline)
            {
                html = await renderer.Dispatcher.InvokeAsync(rendered.ToHtmlString);
                if (html.Contains("Book not found", StringComparison.Ordinal))
                {
                    break;
                }

                await Task.Delay(10);
            }

            Assert.DoesNotContain("First account private title", html);
            Assert.Contains("Book not found", html);
            Assert.Equal(2, books.ReadCount);
        }
        finally
        {
            books.FirstRead.TrySetResult(null);
            await rendered.QuiescenceTask.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class MutableAuthenticationStateProvider(string userId) : AuthenticationStateProvider
    {
        private Task<AuthenticationState> _state = StateFor(userId);

        public override Task<AuthenticationState> GetAuthenticationStateAsync() => _state;

        public void ChangeTo(string nextUserId)
        {
            _state = StateFor(nextUserId);
            NotifyAuthenticationStateChanged(_state);
        }

        private static Task<AuthenticationState> StateFor(string userId) =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "fixture"))));
    }

    private sealed class ControlledBooksApi : IBooksApi
    {
        private int _readCount;

        public int ReadCount => Volatile.Read(ref _readCount);

        public TaskCompletionSource FirstReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SecondReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<GetBookResponse?> FirstRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public CancellationToken FirstReadToken { get; private set; }

        public Task<GetBookResponse?> GetByIdAsync(GetBookRequest req, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _readCount) == 1)
            {
                FirstReadToken = cancellationToken;
                FirstReadStarted.TrySetResult();
                return FirstRead.Task;
            }

            SecondReadStarted.TrySetResult();
            return Task.FromResult<GetBookResponse?>(null);
        }

        public Task<GetBooksResponse> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<SearchBooksResponse> SearchAsync(SearchBooksRequest req, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CreateBookResponse> CreateAsync(CreateBookRequest req, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> UpdateAsync(UpdateBookRequest req, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> DeleteAsync(DeleteBookRequest req, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://example.test/", "https://example.test/books/7/edit");

        protected override void NavigateToCore(string uri, bool forceLoad)
        {
            Uri = ToAbsoluteUri(uri).AbsoluteUri;
            NotifyLocationChanged(false);
        }
    }
}
