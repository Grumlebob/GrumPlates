# Reusable CRUD UI for the Books reference feature

Status: complete. U00–U11 are integrated and verified. The downstream UI port is integrated in RitzauDemo PR #19; its exact-main CI verification is recorded below. Prepared 2026-11-09. This document does not authorize deployment.

## 1. Goal and scope

Replace the Blazor Books shelf/illustrated-page/modal experience with a polished, ordinary CRUD interface that can be adapted quickly to another domain. Build and verify it in GrumPlates first. Then port the relevant UI into RitzauDemo using the explicit handoff below.

The template keeps both frontend profiles and all supported deployment targets. This plan changes the **Blazor Books reference UI**, not the React UI, account capabilities, caching architecture, domain ownership model, or template test policy. React must continue passing its existing checks.

The result includes a searchable list, details, create/edit forms, delete confirmation, and clear loading, empty, validation, permission, not-found, and server-error states. Keep a clean responsive appearance and existing Tailwind styling. Remove book-specific rendering geometry when it has no remaining consumer.

Do not create a metadata-driven CRUD engine, generic repository, second design system, new UI framework, second frontend profile, or optional old/new Books UI switch. Use small concrete feature components. Generalize only a component already used in more than one real place.

The product follow-up is [RitzauDemo: Interview case starter](https://github.com/Grumlebob/RitzauDemo/blob/main/Plans/InterviewCaseStarter.md), integrated on its main branch. RitzauDemo owns product RBAC, account reduction, and test reduction; do not copy those decisions upstream.

## 2. Research completed

Source baseline inspected: GrumPlates `ee9af0b985f86c9568cbf077aeb20336841446de`. Re-inventory before execution; this SHA is an observation, not a required reset target.

| Existing source | Relevant coupling |
| --- | --- |
| `GrumPlates.Client/Features/Books/Routes/Index.razor` | `/` and `/books` render public author books, private user books when signed in, a render-mode badge, and `BookModalHost`. |
| `UserBookcase/UserBookcase.razor` | Private list actions navigate to `/books?bookMode=create`; the list renders through `BookcaseShelf`. |
| `UserBookcase/UserBookcaseState.cs` | Authentication-aware state exposes `ShelfBooks`, mapping DTOs into book-specific display types. Keep identity-change and stale-result protection when removing that mapping. |
| `AuthorBookcase/AuthorBookcaseState.cs` | Public list state also participates in shelf presentation. |
| `BookModal/BookModalHost.razor` | Owns query-modal interpretation, focus/Escape behavior, navigation subscriptions, and component-owned lifecycle. |
| `BookModal/BookModalLifecycle.cs` | Owns loading, cancellation, save/delete, errors, and stale-completion guards. Behavioral responsibilities need replacement even if the class disappears. |
| `BookPage/BookPageEditor.razor` | Uses SVG/`foreignObject` layout, validation, and manual input/change handling. Replace presentation while preserving validation and interactivity gating. |
| `BookPage/*`, `Shared/BookCover*`, `BookcaseShelf*`, `BookSideView` | Book-specific layout, cover artwork, geometry, and rendering conventions are the main simplification candidates. |
| `BooksClientService`, `BooksAntiforgeryHandler`, Core `IBooksApi` | Existing HTTP/contract seam for private CRUD. Components should continue using the contract or feature state. |
| `IAuthorBooksApi`, `AuthorBooksClientService` | Separate public read-only catalog contract. Do not accidentally turn catalog rows into privately editable rows. |
| `GrumPlates.Test/E2E/Features/Books/BooksE2ETests.cs` | Existing workflow depends on modal query strings, shelf selectors, Escape/focus behavior, and actual API operations. Adapt assertions to new behavior. |
| `BookModalNavigationE2ETests`, prehydration/render-mode tests | Navigation and Interactive Auto are real requirements; retain applicable behavior checks as the modal-specific contract is retired. |
| `docs/Features/Blazor.md` | Canonical guide explicitly describes the existing modal lifecycle and will need a small current-behavior correction. |

Preparation completed:

- [x] Locate the coupled UI, state, contracts, and browser verification paths.
- [x] Separate public catalog viewing from private authenticated CRUD.
- [x] Identify prerender, hydration, authentication-change, and cancellation requirements that a markup-only rewrite would lose.
- [x] Define default routes, deletion rules, checks, and a downstream path manifest below.

## 3. Intended experience

### Routes and visibility

Use ordinary routable pages instead of query-driven dialogs:

| Route | Behavior |
| --- | --- |
| `/` and `/books` | Public catalog list; signed-in users also see a clearly separated own-books list. Anonymous users see a sign-in action for private CRUD. |
| `/books/catalog/{id:int}` | Public author-book details; no edit/delete actions. |
| `/books/new` | Authenticated create form. |
| `/books/{id:int}` | Authenticated own-book details. |
| `/books/{id:int}/edit` | Authenticated own-book edit form. |

Keep existing account paths and return URL validation. Product-specific RBAC is applied later during the RitzauDemo port. A User and Admin in the template remain subject to existing owner-scoped private access.

Retire `bookMode`, `bookId`, and `authorBookId` dialog query conventions once all local navigation and test consumers are updated. Do not keep a second modal implementation or indefinite compatibility subsystem. External-link compatibility is not established by this plan; if actual consumers are discovered, record that evidence and choose a bounded mapping before removing their routes.

### Lists and search

- Use readable rows with title, author, and explicit details/edit actions where permitted. Use responsive rows or cards on narrow screens without horizontal overflow.
- Keep public and private data sources visibly separate. Search applies to the relevant loaded collection and has a visible label and clear action.
- Reuse current list APIs for the presentation change. Inspect current payload bounds first. Local filtering is suitable only for the existing bounded demo collection; it is not server pagination or a scalability claim.
- Do not silently truncate records. If current data is not bounded enough for a local filter, separate server search/pagination into a deliberately reviewed prerequisite rather than claiming that a UI filter solves it.
- Distinguish no records, no search matches, loading, and failure. A failed request must not appear as an empty successful result.

### Forms, details, and deletion

- Use an ordinary labeled `EditForm`, current validation rules, validation summary/messages, and normal keyboard submission.
- Reuse a concrete `BookForm.razor` between create and edit. Title, author, and URL remain sample domain fields; do not invent future case fields.
- Keep user input after a failed save. Disable duplicate submissions while busy and disable interactive controls until `RendererInfo.IsInteractive`.
- Keep cancel safe: no persistence, predictable return path, and no accidental form submission.
- Details show a normal heading and field values. A valid external URL uses safe link behavior; never inject raw HTML.
- Delete requires explicit confirmation containing the selected title. A simple inline confirmation is sufficient; a custom modal/focus framework is unnecessary.
- Show not-found and permission outcomes clearly. Do not reveal another user's private record metadata.
- After successful mutation, refresh relevant feature state using existing contracts and invalidation; Back/reload must not resurrect stale state.

### Visual quality

Use the existing application shell, Tailwind tokens, spacing, readable typography, focus rings, and button conventions. Favor clear content and actions over book illustrations. Include a restrained header, useful count/search context, and stable loading space where appropriate. Keep native labels, semantic links/buttons, visible keyboard focus, and touch-friendly controls. No marketing page or new theme system is required.

## 4. Non-negotiable behavior

- Blazor product routes work with SSR/prerender, initial server interactivity, and later WebAssembly interactivity under Interactive Auto.
- Do not force Interactive Server to hide a browser/SSR issue. Verify the actual renderer rather than the mode badge text.
- Components call Core API contracts or feature state, not raw `HttpClient`.
- Keep direct server and browser implementations consistent. Continue using current account/CSRF handlers and owner-scoped server queries.
- On logout or identity change, clear private rows and in-flight results before another user's data can be displayed.
- Retain cancellation and stale-completion guards when moving state out of the modal lifecycle. Dispose subscriptions and cancellation sources.
- Keep public prerender data bounded. Do not embed private response graphs in `PersistentComponentState`.
- Keep caches, invalidation, backend endpoint names/contracts, seeded catalog behavior, and database schema unchanged unless a separately reviewed prerequisite is needed.
- Rebuild and commit `GrumPlates/wwwroot/tailwind.css` for Razor class/style changes using the existing client lockfile.
- Preserve both frontend profiles, shared-host safeguards, required CI, and all deployment targets.

## 5. Execution packets

Complete each packet's verification before checking it off. Record PR/head/integration evidence in section 9. Start with a working route, then replace remaining behavior in bounded steps. Temporary internal wiring during a branch is acceptable; do not ship two competing Books experiences.

### U00 — Establish ownership and current baseline

- [x] Read applicable `AGENTS.md`, `docs/Requirements.md`, `docs/Features/Blazor.md`, `docs/Test.md`, and `docs/Testing/Browser.md`.
- [x] Inspect actual branch/worktree ownership, dirty tracked/untracked/ignored files, open PR dependencies, and current main SHA. Create an owned task branch without disturbing another session.
- [x] Inventory current Books routes, navigation links, API contracts, test selectors, client/server DI registrations, and relevant assets.
- [x] Record any source differences from section 2. Do not restore the inspected SHA over newer work.

Exit: explicit current base and a list of exact consumers to update. The implementation branch started from `origin/main` at `b2dd255cea8c95f496b241b5517d246938e2d681`, after the separately reviewed private search prerequisite. The source SHA in section 2 remains a historical baseline through the Books feature. At kickoff, open PRs 451–453 targeted `main` and none depended on this work. The original untracked plan in the primary checkout was preserved. Implementation is isolated in branch `task/reusable-crud-ui-implementation-20261109` in a sibling task worktree.

### U01 — Record the UI replacement and deletion map

- [x] Confirm the default route table above against existing route collisions, especially numeric private IDs and literal `new`/`catalog` segments.
- [x] Map each modal lifecycle responsibility to the new list/detail/form state owner: load, identity check, cancellation, save/delete, errors, state refresh, disposal.
- [x] Map each old component/helper/test/asset to retain, adapt, or delete-after-last-consumer.
- [x] Inspect list payload behavior and decide whether local search is valid for the bounded sample. Keep a server search prerequisite separate if needed.
- [x] Put the compact path mapping and any justified route change in the implementation PR, not a new permanent design manual.

Exit: a markup rewrite cannot silently discard behavior, and all planned deletions have a consumer check. `/books/new` does not collide with `/books/{id:int}`, and `/books/catalog/{id:int}` does not collide with the private integer route. `/books/author/{SeedKey}` has no in-repository Blazor navigation consumer; retain a bounded redirect to the new catalog details route while removing its modal target. `BooksServerService` loads every private row with `ToListAsync` and has no record or payload bound, while private CRUD can grow that collection; client-only search is therefore not a safe substitute for pagination. The separately reviewed, owner-scoped search/pagination API is integrated on the existing `/api/books` resource with per-query cache keys and existing owner-list invalidation tags. The static public author catalog has six seeded entries and can use local filtering.

### U02 — Decouple list state from shelf geometry

- [x] Expose current contract DTOs or a small concrete row model from private/public list state instead of requiring `BookcaseBook`/`ShelfBooks` for every view.
- [x] Keep user identity/version checks, error handling, cancellation, and mutation refresh behavior.
- [x] Update state tests to assert records, ownership/identity transitions, and stale result rejection rather than cover layout.
- [x] Keep existing UI working while the branch introduces these state changes; do not leave broken consumers between commits intended for review.

Exit: a normal row component can consume list state without cover geometry or a generic mapper framework.

### U03 — Implement the public catalog list and details

- [x] Add concrete list-row/detail components or pages under `Features/Books`; keep routes under `Routes`.
- [x] Implement public catalog list, labeled search, clear filter, no-results/empty/error/loading states, and normal detail links.
- [x] Add `/books/catalog/{id:int}` details using `IAuthorBooksApi`. Handle missing IDs and failed loads without showing edit/delete actions.
- [x] Verify anonymous SSR includes useful public content and no private data. Keep hydration controls disabled as required.
- [x] Verify both wide and narrow layouts with long titles, missing author, and absent URL.

Exit: public browsing works without shelf rendering or query modals.

### U04 — Implement the private list and details

- [x] Add private own-books rows and actions through `IBooksApi`/feature state; add `/books/{id:int}` details.
- [x] Add authorization to private routes and preserve secure return navigation for sign-in.
- [x] Make signed-out/private-loading/error/empty/no-match states distinct. Do not flash the previous user's list after logout/login.
- [x] Keep explicit edit links and prepare inline deletion state without executing deletes yet.
- [x] Verify owner-visible rows, another user's not-found response, direct route refresh, and Back navigation.

Exit: authenticated reading works in direct server and browser execution paths.

### U05 — Implement a normal create form

- [x] Add `/books/new` and a concrete reusable `BookForm.razor` using current DTO validation and normal form semantics.
- [x] Add proper labels, field messages, validation summary, Save/Cancel controls, and busy/interactivity gating.
- [x] Submit through current contracts. Retain entered values and show a bounded useful error if a request fails.
- [x] On success refresh own-book state and navigate predictably to the created detail or list; choose one behavior and use it consistently.
- [x] Verify valid create/reload, invalid title/URL, keyboard submit, cancel without mutation, and retry after a save failure.

Exit: create no longer requires the illustrated editor or modal lifecycle.

### U06 — Implement edit and delete confirmation

- [x] Add `/books/{id:int}/edit`, load only the current user's record, and reuse `BookForm` with an independent edit model.
- [x] Cancel does not mutate cached/list DTO instances. A failed save leaves the user's form input intact.
- [x] Add explicit inline delete confirmation with title, Cancel, and busy state. Do not send a delete on the first request-to-delete click.
- [x] Handle a record deleted elsewhere, denied operation, and API failure without reporting false success.
- [x] Refresh state after update/delete and verify cache-backed normal reads display the changes.

Exit: the complete private CRUD workflow uses ordinary pages and controls.

### U07 — Switch navigation and remove the old presentation

- [x] Switch `/` and `/books`, list/detail/edit/create links, and author detail links to the new route contract.
- [x] Remove `BookModalHost`, parser/lifecycle, illustrated `BookPage` components, cover/shelf/side-view helpers, and mapper code only where no remaining consumer exists.
- [x] Search the full repository for old type names, query strings, test selectors, CSS/JS hooks, asset paths, and source-test assertions. Include tests, docs, and profile composition.
- [x] Inspect book assets before removal; preserve seeded sample data or files with a remaining React/public/API consumer.
- [x] Remove only unused presentation dependencies. No wholesale package upgrades or asset-directory deletion.

Exit: one Books UI remains; no stale route convention or dead geometry dependency is needed by the new UI.

### U08 — Update focused UI verification

- [x] Adapt `BooksE2ETests` into a focused public-read plus authenticated CRUD journey using semantic selectors and a few stable test IDs.
- [x] Replace modal-specific navigation tests with direct link/refresh/Back coverage where those user actions remain meaningful.
- [x] Remove geometry/modal tests only with the deleted behavior; retain actual state, ownership, authentication-transition, API, cache, and security tests.
- [x] Adapt prehydration checks to the new Save/Delete/search controls as applicable. New markup must not silently bypass their assertions.
- [x] Keep actual render-mode verification for initial server interactivity and later WASM. An existing server component does not switch runtime merely because WASM downloaded.
- [x] Cover validation, failed save/retry, delete cancellation, logout state clearing, and narrow viewport behavior through existing suitable boundaries. Avoid adding many tiny markup tests.

Exit: tests describe the new user contract without losing retained security or render-mode coverage. Template-wide test reduction is outside this plan.

### U09 — Rebuild styles and inspect rendered results

- [x] From `GrumPlates.Client`, run the lockfile-based install and `npm run css:build` using the supported Node environment.
- [x] Commit the generated `GrumPlates/wwwroot/tailwind.css` when changed. Do not hand-edit generated CSS.
- [x] Run the application and inspect list, details, form, confirmation, loading/empty/error states, and keyboard focus at narrow and wide widths.
- [x] Verify long values wrap, labels/messages fit, controls are reachable, and no book-layout geometry or SVG clipping survives accidentally.
- [x] Run the existing browser/Lighthouse guidance appropriate to this material public-layout change. Do not add permanent screenshot artifacts or make a new performance claim without measurements.

Exit: rendered UI is clean and usable, not merely compilable.

### U10 — Minimal documentation and both-profile validation

- [x] Correct the modal-specific paragraph/source links in `docs/Features/Blazor.md` and any directly stale feature links. Keep examples short.
- [x] Update requirements or audit rules only when checked behavior actually changes. Do not delete an audit requirement to bypass a failure.
- [x] Review API/Core/backend diffs; presentation changes should not have rewritten backend contracts, cache semantics, identity, or migrations.
- [x] Run every command in the current `docs/Test.md#local-gate`, including the both-profile validation and deployment audits required by `AGENTS.md`.
- [x] Keep `dotnet build` and `dotnet test` sequential in each checkout. Run affected opt-in browser journeys separately using current documented configuration.
- [x] Verify React remains unchanged in behavior and passes its checks. Do not disable its composition because this plan targets Blazor.

Exit: full local gate and applicable browser checks pass before pushing.

### U11 — Integrate and prepare the downstream handoff

- [x] Open a focused GrumPlates PR describing the simpler interaction and relevant verification. Split state preparation from replacement UI only if each integrated PR stays functional.
- [x] Require green `build-test-push` on the exact PR head. Retarget dependent PRs before merging/deleting their base.
- [x] Fetch and verify integrated main content and required exact-main CI; record full SHAs, PR URLs, and CI run/attempt references.
- [x] Populate section 7 with exact integrated paths and commits. Mark any mixed UI/backend commit for selective adaptation rather than blind cherry-pick.
- [x] Integrate the verified handoff record from RitzauDemo PR #19 and record the downstream integration and exact-main verification.
- [x] Clean owned branches and Docker resources under the closeout rules. Record deleted branch SHAs and any retained worktree dependency.

Closeout: branches `task/reusable-crud-ui-20261109`, `task/reusable-crud-ui-implementation-20261109`, `task/reusable-crud-ui-closeout-20261109`, and `task/fix-responsive-book-e2e-20261109` were deleted at full SHAs `48016999df1f68d339236480d859a114077a1553`, `39349002c1008b5b9196ef8e5f9321272e4bb729`, `fb58c9650b3b1b188f57824766fedea2df7f3969`, and `e188e7c93fa289882ffa23d86e80d9f04af61434`, respectively, after content was verified against their integrated commits. The owned implementation, docs-closeout, and responsive-fix worktrees were removed from another checkout after the visual/browser project and its containers had stopped. The scoped `grumplates-ui-visual-20261109` Compose containers, network, database volume, and web image were removed. The task's original private `.env` was preserved under ignored `Plans.local/ReusableCrudUi/.env`; the primary checkout's private files were left intact.

Exit: upstream UI and the downstream handoff are integrated and verified. The product port is recorded in [RitzauDemo PR #19](https://github.com/Grumlebob/RitzauDemo/pull/19), merged at `caaed037b988cd195df1c78d6b485137b058fac2`. Its exact PR-head `build-test-push` run `42704763594` and CodeQL run `42704763557` passed. Exact-main CI run `42708863875` passed on that same SHA after the squash push had been skipped by an inherited `[skip ci]` marker. No deployment was performed.

## 6. Verification checklist

Use the repository's current canonical local gate, not a shortened command list copied into this plan. Focused checks during packets supplement the required full gate before code pushes.

- Anonymous: list/detail render public content, private actions require sign-in, no private payload is prerendered into public state.
- Signed in: own list, create, details, edit, cancel, delete confirmation, delete, reload, and Back work.
- Validation/errors: errors preserve input; requests do not falsely show success; missing records and denied access remain distinct from empty lists.
- Identity: logout and a different login clear private rows and stale pending responses.
- Cache: writes still invalidate and subsequent normal reads display fresh data; no production caching switch is disabled for the demo.
- Rendering: SSR/prerender, initial server interactivity, and later actual WASM behavior work under Interactive Auto.
- Accessibility/layout: labeled fields/search, keyboard submit, focus, touch controls, narrow widths, and long data are usable.
- Regression: React and all required template gates stay green; no identity or database migration surprise.

## 7. Port manifest for RitzauDemo

This list describes how to adapt the integrated upstream changes, not permission to overwrite a destination directory.

| Source change category | Downstream treatment |
| --- | --- |
| New `GrumPlates.Client/Features/Books/Routes/*` list/detail/create/edit pages | Adapt namespaces, imports, routes, and product-specific role visibility. |
| Concrete form/list/detail components under the Books feature | Port the relevant source; avoid copying unused public/private variants. |
| Updated public/private feature state and row models | Adapt current contract signatures and retain product authentication/cancellation behavior. |
| Client/server UI-state DI registrations and `_Imports.razor` | Apply the minimal matching changes in current product composition; preserve its handler wiring. |
| Removed modal/shelf/cover/page helpers and their assets | Remove downstream equivalents only after checking all destination consumers. |
| Tailwind class/style sources | Port source changes; rebuild destination CSS with its own lockfile. |
| Focused state/browser tests and selectors | Port applicable scenarios; preserve the product's small test budget and its single architecture test. |
| Core/API changes, if any prerequisite genuinely required them | Review individually against product slices, caching, endpoint RBAC, and the selected role matrix; never overwrite them wholesale. |
| Template docs, React, CI, deployment, unrelated accounts, historical plans | Do not copy merely because they share an upstream commit. Correct only destination facts affected by the actual port. |

### Exact upstream source manifest

Added by the UI implementation:

- `GrumPlates.Client/Features/Books/Routes/AuthorBookDetails.razor`
- `GrumPlates.Client/Features/Books/Routes/BookCreate.razor`
- `GrumPlates.Client/Features/Books/Routes/BookEdit.razor`
- `GrumPlates.Client/Features/Books/Routes/UserBookDetails.razor`
- `GrumPlates.Client/Features/Books/Shared/BookDetailsView.razor`
- `GrumPlates.Client/Features/Books/Shared/BookForm.razor`
- `GrumPlates.Client/Features/Books/Shared/BookFormModel.cs`
- `GrumPlates.Client/Features/Books/Shared/BookListRow.razor`
- `GrumPlates.Test/E2E/Features/Books/BookNavigationE2ETests.cs`
- `GrumPlates.Test/Features/Books/Client/BookEditAuthenticationTests.cs`

Updated by the UI implementation:

- `GrumPlates.Client/Features/Books/AuthorBookcase/AuthorBookcase.razor`
- `GrumPlates.Client/Features/Books/AuthorBookcase/AuthorBookcaseState.cs`
- `GrumPlates.Client/Features/Books/Routes/AuthorDetails.razor`
- `GrumPlates.Client/Features/Books/Routes/Index.razor`
- `GrumPlates.Client/Features/Books/UserBookcase/UserBookcase.razor`
- `GrumPlates.Client/Features/Books/UserBookcase/UserBookcaseLoginPrompt.razor`
- `GrumPlates.Client/Features/Books/UserBookcase/UserBookcaseState.cs`
- `GrumPlates.Client/Features/Books/_Imports.razor`
- `GrumPlates.Test/E2E/AppShell/PreHydrationControlsE2ETests.cs`
- `GrumPlates.Test/E2E/Features/Books/BooksE2ETests.cs`
- `GrumPlates.Test/E2E/VisualRegression/VisualSnapshotE2ETests.cs`
- `GrumPlates.Test/Features/Books/Client/UserBookcaseStateTests.cs`
- `GrumPlates/wwwroot/app.css`
- `GrumPlates/wwwroot/tailwind.css` (generated)
- `Scripts/Quality/sonar_report.py`
- `Scripts/Quality/tests/fixtures/sonar/component-tree.json`
- `Scripts/Quality/tests/fixtures/sonar/issues-search.json`
- `Scripts/Quality/tests/test_sonar_report.py`
- `docs/Architecture.md`
- `docs/Features/Blazor.md`
- `Plans/ReusableCrudUi.md`

Removed after checking for remaining consumers:

- `GrumPlates.Client/Features/Books/BookModal/BookModalHost.razor`
- `GrumPlates.Client/Features/Books/BookModal/BookModalLifecycle.cs`
- `GrumPlates.Client/Features/Books/BookModal/BookModalRouteState.cs`
- `GrumPlates.Client/Features/Books/BookPage/BookPageCloseControl.razor`
- `GrumPlates.Client/Features/Books/BookPage/BookPageEditor.razor`
- `GrumPlates.Client/Features/Books/BookPage/BookPageEditorModel.cs`
- `GrumPlates.Client/Features/Books/BookPage/BookPageFrame.razor`
- `GrumPlates.Client/Features/Books/BookPage/BookPageTextLayout.cs`
- `GrumPlates.Client/Features/Books/BookPage/BookPageView.razor`
- `GrumPlates.Client/Features/Books/Shared/BookCoverArtwork.razor`
- `GrumPlates.Client/Features/Books/Shared/BookCoverDesignCatalog.cs`
- `GrumPlates.Client/Features/Books/Shared/BookCoverGeometry.cs`
- `GrumPlates.Client/Features/Books/Shared/BookCoverPageTabs.razor`
- `GrumPlates.Client/Features/Books/Shared/BookCoverRenderer.razor`
- `GrumPlates.Client/Features/Books/Shared/BookCoverTextLayout.cs`
- `GrumPlates.Client/Features/Books/Shared/BookCoverTheme.cs`
- `GrumPlates.Client/Features/Books/Shared/BookSideView.razor`
- `GrumPlates.Client/Features/Books/Shared/BookcaseBook.cs`
- `GrumPlates.Client/Features/Books/Shared/BookcaseShelf.razor`
- `GrumPlates.Client/Features/Books/Shared/BookcaseShelfItems.cs`
- `GrumPlates.Client/Features/Books/UserBookcase/UserBookcaseBookMapper.cs`
- `GrumPlates.Test/E2E/Features/Books/BookModalNavigationE2ETests.cs`
- `GrumPlates.Test/Features/Books/Client/BookCoverGeometryTests.cs`
- `GrumPlates.Test/Features/Books/Client/BookModalHostAuthenticationTests.cs`
- `GrumPlates.Test/Features/Books/Client/BookModalLifecycleTests.cs`
- `GrumPlates.Test/Features/Books/Client/BookModalRouteStateTests.cs`
- `GrumPlates.Test/Features/Books/Client/BookTitleLayoutTests.cs`
- `GrumPlates.Test/Features/Books/Client/BookcaseShelfItemsTests.cs`

Route contract: `/` and `/books` show the public catalog and the signed-in user's private list; `/books/catalog/{id:int}` is public detail; `/books/author/{SeedKey}` is a bounded legacy redirect; `/books/new`, `/books/{id:int}`, and `/books/{id:int}/edit` are authenticated create, detail, and edit routes. The UI implementation changes no Core/API contracts, backend services, DI registrations, or migrations. It consumes the owner-scoped `IBooksApi.SearchAsync(SearchBooksRequest)` prerequisite from PR #454 and the existing `IAuthorBooksApi`; downstream code must adapt these signatures and preserve owner scoping.

Rebuild generated CSS from `GrumPlates.Client` with `npm ci && npm run css:build`. Applicable verification covers `BooksE2ETests`, `BookNavigationE2ETests`, `BookEditAuthenticationTests`, `UserBookcaseStateTests`, pre-hydration controls, actual render-mode checks, both frontend profiles, and the full local gate. No upstream dependency remains beyond integrated PR #454. RitzauDemo PR #19 applies the product role matrix (User read-only, Admin own-book CRUD) and verifies the actual render modes.

The source handoff record is complete in [RitzauDemo PR #19](https://github.com/Grumlebob/RitzauDemo/pull/19): exact upstream base/head/integrated SHAs and CI runs, path manifest, contracts, routes, regenerated asset instructions, applicable tests, and product-specific adaptation notes. PR #19 is merged; its required PR-head checks passed and the exact-main `build-test-push` run passed on the integrated SHA.

The downstream port is recorded in RitzauDemo's `Plans/InterviewCaseStarter.md`, packets R14–R16, and was accepted only after verifying destination ownership rules, both seeded roles, caching, and actual Interactive Auto render modes.

## 8. Blockers and decisions

- Upstream implementation, downstream handoff, and verification are complete. RitzauDemo PR #19 is merged at `caaed037b988cd195df1c78d6b485137b058fac2`; exact-main `build-test-push` run `42708863875` passed. CodeQL passed on the exact PR head in run `42704763557`.
- Default design is replacement of the Blazor Books UI, with public catalog/private list separation and ordinary routes as specified. React remains supported and unchanged.
- Do not wait for RitzauDemo RBAC selection to implement the upstream UI; that is a downstream adaptation concern.
- Do not dispatch a deployment as part of this work. Deployment authorization is separate if later requested.
- A discovered real dependency, failing gate, or in-use route gets a concrete diagnosis and next action. Do not invent manual blockers before such evidence exists.

## 9. Execution ledger and completion

| Packet / PR | Source and integrated full SHA | Verification | Downstream handoff |
| --- | --- | --- | --- |
| U00 planning and baseline / [PR #454](https://github.com/Grumlebob/GrumPlates/pull/454) | `Plans/ReusableCrudUi.md` integrated as `b2dd255cea8c95f496b241b5517d246938e2d681` | Baseline, route, payload, and consumer inventory recorded; Markdown whitespace check passed | The UI path manifest and explicit downstream map are complete in sections 6–7. |
| U01 private search prerequisite / [PR #454](https://github.com/Grumlebob/GrumPlates/pull/454) | PR head `48016999df1f68d339236480d859a114077a1553`; squash integrated on `origin/main` as `b2dd255cea8c95f496b241b5517d246938e2d681` | Full local gate passed; exact-head `build-test-push` run `42658249521` and CodeQL run `42658249876` passed; exact-main CI run `42661503975` and CodeQL run `42661504202` passed | Owner-scoped private search/pagination contract and endpoint are available for the Blazor list and downstream adaptation. |
| U02–U10 implementation / [PR #455](https://github.com/Grumlebob/GrumPlates/pull/455) | Base `b2dd255cea8c95f496b241b5517d246938e2d681`; PR head `39349002c1008b5b9196ef8e5f9321272e4bb729`; squash-integrated on `origin/main` as `555cc103e33ae18838129cd79be8ec738a8a6d68`; deleted branch SHA `39349002c1008b5b9196ef8e5f9321272e4bb729` | Full local gate passed. Exact-head `build-test-push` [run 42683644193, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42683644193) and CodeQL [run 42683644261, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42683644261) passed. Exact-main `build-test-push` [run 42686724906, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42686724906) and CodeQL [run 42686724951, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42686724951) passed. | [RitzauDemo PR #19](https://github.com/Grumlebob/RitzauDemo/pull/19) records the complete R14 handoff and adaptation map; port merged on product main and exact-main CI passed. |
| Verified handoff note / [PR #456](https://github.com/Grumlebob/GrumPlates/pull/456) | Base `555cc103e33ae18838129cd79be8ec738a8a6d68`; PR head and deleted branch SHA `fb58c9650b3b1b188f57824766fedea2df7f3969`; integrated as `9ae57ec2683093f73fa38d10c820af099589da88` | Markdown-only diff; `[skip ci]` docs exception; `git diff --check` passed | Recorded the then-pending product handoff; updated closeout now records PR #19's merge and verification. |
| Responsive browser fix / [PR #457](https://github.com/Grumlebob/GrumPlates/pull/457) | Base `9ae57ec2683093f73fa38d10c820af099589da88`; PR head and deleted branch SHA `e188e7c93fa289882ffa23d86e80d9f04af61434`; integrated on `origin/main` as `6076953820628d3220725d183b022dfca83789fd` | Full local gate passed; responsive snapshots passed at 1441×901 and 391×843. Exact-head `build-test-push` [run 42697999502, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42697999502) and CodeQL [run 42697999781, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42697999781) passed. Exact-main CI [run 42701077135, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42701077135) and CodeQL [run 42701076918, attempt 1](https://github.com/Grumlebob/GrumPlates/actions/runs/42701076918) passed. | Product PR #19 ports this verified mobile navigation behavior with role-aware UI and browser coverage. |

For each implementation PR, record exact head, local gate outcome, exact-head CI, main integration SHA, exact-main CI, and cleanup branch SHAs here or in the PR. Plan completion requires the new UI integrated on GrumPlates main, applicable checks green, dead presentation removed, minimal docs accurate, port manifest populated, and owned-resource closeout. RitzauDemo port completion is recorded in its own plan, not falsely claimed here.
