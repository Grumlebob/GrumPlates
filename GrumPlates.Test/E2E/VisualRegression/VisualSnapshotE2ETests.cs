using GrumPlates.Test.E2E.Support;
using GrumPlates.Test.TestSupport.Integration;
using Microsoft.Playwright;

namespace GrumPlates.Test.E2E.VisualRegression;

[Collection(TestCollectionNames.E2E)]
public sealed class VisualSnapshotE2ETests : BlazorE2ETestBase
{
    [Fact(Skip = "Set RUN_E2E=1 to run Playwright E2E tests.", SkipUnless = nameof(E2ETestGuard.IsEnabled), SkipType = typeof(E2ETestGuard))]
    [Trait("Category", "E2E")]
    public async Task KeyScreens_CanBeCapturedForReview()
    {
        await RunWithFailureScreenshotAsync(async () =>
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var title = $"Snapshot Book {suffix}";
            var author = $"Snapshot Author {suffix}";
            var url = $"https://example.test/books/{suffix}";

            await GoHomeAndWaitForInteractivityAsync();
            await Expect(Page.GetByTestId("author-book-list")).ToBeVisibleAsync();
            var publicBook = Page.GetByTestId("author-book-list").GetByRole(AriaRole.Link).First;
            await publicBook.ClickAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            await CaptureAsync("books-catalog-details");
            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Back to catalog" }).ClickAsync();
            await Expect(Page.GetByTestId("private-books-sign-in")).ToContainTextAsync("Sign in to create, edit, and remove books");
            await CaptureAsync("home");

            TrackCreatedBook(title, url);

            await GoToAsync("/Account/Login");
            await CaptureAsync("login");

            await GoToAsync("/Account/Register");
            await CaptureAsync("register");
            await SignInAsSeededUserAsync();
            await GoHomeAndWaitForInteractivityAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Your books" })).ToBeVisibleAsync();

            await Page.GetByTestId("add-book").ClickAsync();
            await Expect(Page.GetByTestId("book-form")).ToBeVisibleAsync();
            await CaptureAsync("books-create");

            await Page.GetByTestId("book-title").FillAsync(title);
            await Page.GetByTestId("book-author").FillAsync(author);
            await Page.GetByTestId("book-url").FillAsync(url);
            await Page.GetByTestId("book-save").ClickAsync();

            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = title, Exact = true })).ToBeVisibleAsync();
            await CaptureAsync("books-details");

            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Back to your books" }).ClickAsync();
            var bookLink = Page.GetByTestId("user-book-list").GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = title, Exact = true });
            await Expect(bookLink).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await bookLink.ClickAsync();
            await Page.GetByRole(AriaRole.Link, new PageGetByRoleOptions { Name = "Edit book" }).ClickAsync();
            await Expect(Page.GetByTestId("book-edit-page")).ToBeVisibleAsync();
            await Expect(Page.GetByTestId("book-title")).ToHaveValueAsync(title);
            await CaptureAsync("books-edit");

            await GoToAsync("/Account/Manage");
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = "Profile" }))
                .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });
            await CaptureAsync("account-manage");

            await GoToAsync("/books/99999999");
            await Expect(Page.GetByTestId("user-book-not-found")).ToBeVisibleAsync();
            await CaptureAsync("not-found");
        });
    }

    private async Task CaptureAsync(string name)
    {
        var snapshotDirectory = GetPlaywrightArtifactPath("Snapshots");
        Directory.CreateDirectory(snapshotDirectory);

        await Page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(snapshotDirectory, $"{GetViewportLabel()}-{name}.png"),
            FullPage = true
        });
    }

    private static string GetViewportLabel()
    {
        var width = Environment.GetEnvironmentVariable("E2E_VIEWPORT_WIDTH");
        var height = Environment.GetEnvironmentVariable("E2E_VIEWPORT_HEIGHT");

        if (string.IsNullOrWhiteSpace(width) || string.IsNullOrWhiteSpace(height))
        {
            return "1280x900";
        }

        return $"{width}x{height}";
    }
}
