using GrumPlates.Test.E2E.Support;
using GrumPlates.Test.TestSupport.Integration;
using Microsoft.Playwright;

namespace GrumPlates.Test.E2E.Features.Books;

[Collection(TestCollectionNames.E2E)]
public sealed class BookNavigationE2ETests : BlazorE2ETestBase
{
    [Fact(Skip = "Set RUN_E2E=1 to run Playwright E2E tests.", SkipUnless = nameof(E2ETestGuard.IsEnabled), SkipType = typeof(E2ETestGuard))]
    [Trait("Category", "E2E")]
    public async Task PublicBookLinksSupportDirectRefreshAndBrowserBack()
    {
        await RunWithFailureScreenshotAsync(async () =>
        {
            await Page.SetViewportSizeAsync(1440, 900);
            await GoHomeAndWaitForInteractivityAsync();
            await AssertNoPageHorizontalOverflowAsync("public catalog at 1440px");
            var rows = Page.GetByTestId("author-book-list").GetByRole(AriaRole.Article);
            Assert.True(await rows.CountAsync() >= 2, "Two public catalog rows are required for route navigation coverage.");

            var firstBook = rows.First.GetByRole(AriaRole.Link).First;
            var secondBook = rows.Nth(1).GetByRole(AriaRole.Link).First;
            var firstHref = await firstBook.GetAttributeAsync("href");
            var secondHref = await secondBook.GetAttributeAsync("href");
            Assert.NotNull(firstHref);
            Assert.NotNull(secondHref);
            Assert.NotEqual(firstHref, secondHref);

            await firstBook.ClickAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            Assert.Equal(firstHref, new Uri(Page.Url).AbsolutePath);
            var firstTitle = await Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Level = 1 }).InnerTextAsync();

            await Page.GoBackAsync();
            await Expect(Page.GetByTestId("author-book-list")).ToBeVisibleAsync();
            await secondBook.ClickAsync();
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            Assert.Equal(secondHref, new Uri(Page.Url).AbsolutePath);

            await Page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await Expect(Page.GetByTestId("book-details")).ToBeVisibleAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new PageGetByRoleOptions { Name = firstTitle, Exact = true })).ToHaveCountAsync(0);
            await Page.SetViewportSizeAsync(390, 844);
            await AssertNoPageHorizontalOverflowAsync("public catalog details at 390px");
        });
    }
}
