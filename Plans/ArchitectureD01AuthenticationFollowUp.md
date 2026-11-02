# D01 follow-up: changed authentication on the same modal route

Status: characterization, bridge, focused feedback, full local gate and isolated final-image browser acceptance passed at `f3c7a4d1121aed6eff84bf32ed09264ba1ff02cc`, and again after rebasing onto the minimatch audit fix at `28e49c224990f53078af7b35ebeaf5f6592e03d8`. Exact-head/main CI, reviewed integration and closeout remain. Production deployment is outside this package.

## Requirement and baseline

Portfolio's architecture goal requires modal state to preserve authorization and reject stale responses when accounts change. Original [D01 PR 402](https://github.com/Grumlebob/GrumPlates/pull/402) separated the component-owned lifecycle and verified route changes, stale authentication waits, save/delete completion and disposal. Current baseline `25c38cbbe0f908302b82b22e8d74296679de0b9a` contains that accepted source plus documentation reconciliation.

The final bounded L5 review could not identify a same-route account-change test. Primary source inspection confirms the missing bridge: `BookModalHost` reloads only during initialization and URI changes. Its cascading `AuthenticationStateTask` can change on the same route without calling the lifecycle's existing `LoadAsync` invalidation/cancellation path. The host sits outside the Books page's `AuthorizeView`; server authentication revalidation can notify a replacement state. An old private load or operation therefore lacks this supersession signal. This finding is not dismissed merely because normal login/logout also performs a document navigation.

The primary separately resolved the A01 seed-evidence ambiguity: existing `LocalLoginAccountSeedingRulesTests.IsSeedingEnabled_IsOnByDefaultOnlyInDevelopment` has six explicit environment/flag rows, and `AdminAccountSeedTests.DisabledSeed_ReturnsWithoutResolvingDatabaseServices` protects the disabled configured seed. No seed change or duplicate test is needed.

## Finite ownership and intended correction

| Path | Responsibility |
| --- | --- |
| `GrumPlates.Client/Features/Books/BookModal/BookModalHost.razor` | Continue owning rendering, URI parsing, cascading authentication and component lifetime. Detect a replacement authentication task in parameter lifecycle and call the existing route load once; do not duplicate the initial load. |
| `GrumPlates.Test/Features/Books/Client/BookModalHostAuthenticationTests.cs` | Render the real host with the existing ASP.NET Core static renderer and a mutable cascading authentication provider. Block an old private load, notify a replacement account/anonymous state at the same URI, then release the old result and verify cancellation and absence of stale content. Verify initial prerender loads once without browser focus services. |
| Existing `BookModalLifecycleTests.cs`, only if an uncovered operation observation needs it | Reuse existing controlled save/delete/disposal cases; add no private-field or DTO-construction assertions. |
| This plan | Own the concrete finding, baseline/after evidence, review, integration and closeout record. |

The component remains the bridge; `BookModalLifecycle` remains unregistered and owned/disposed by each component instance. Reuse its current version/cancellation guards and request snapshots. Preserve every URL, model, message, cache operation, server authorization rule, DI lifetime, browser focus/Escape behavior, rendering mode and generated asset. Add no authentication provider subscription when the existing cascading parameter lifecycle already supplies changes.

The test project already references `Microsoft.AspNetCore.App`; no new test package is needed. [HtmlRenderer.BeginRenderingComponent](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.web.htmlrenderer.beginrenderingcomponent?view=aspnetcore-10.0) permits observing initial asynchronous render output and awaiting `QuiescenceTask` after controlled completion. Static rendering does not replace the required real-browser focus/hydration checks. All fake API responses and accounts are synthetic.

## Verification and closeout

Test-only source `f636e2665a8dd1e33f526ff1715272698842bc6d` passed native restore, complete solution formatting and fresh Release compilation with zero warnings/errors. The broad BookModal filter selected nineteen results: sixteen passed, both new account-change/sign-out cases failed, and one existing opt-in browser case was NotExecuted. Initial static prerender and every pre-existing selected modal unit case passed. Both new failures occur at the barrier waiting for a second read after authentication notification: the unchanged host never starts that read. The bounded finally block releases the old load and awaits renderer completion, leaving no hung test or unowned process. All seven raw diagnostic payloads and their source hashes are preserved. This deliberate failing characterization is evidence of the uncovered requirement, not a successful application gate.

The primary accepts a seven-line bridge: remember the authentication task used by each route load, and reload through the existing lifecycle only when parameter processing receives a different task. Initialization records that task before its asynchronous load, so the initial parameter phase does not issue a second request. URI changes keep their existing path. A replacement task now clears stale content and cancels active load/operation state through the already tested lifecycle owner. No lifecycle, API, identity, rendering, CSS or storage policy changes.

First execute the focused BookModal family on test-only source, retaining the old-route/disposal results and the new same-route failure. Correct only the host bridge after confirming that behavior. Then repeat focused feedback, review the exact lifecycle/source delta and run the complete prescribed local gate for both frontend profiles and all static checks before push. CSS classes do not change. Verify actual final-image modal/account/browser behavior with zero required skips.

Require exact-head `build-test-push`, reviewed guarded squash with an explicit non-skip message, fetched integrated content and required exact-main CI/final payload receipt. Retain failed characterization and successful raw evidence with exact identities and hashes. Update the owning PR and Portfolio index, archive compact unique evidence and close only this task's worktree, branches, source/build/smoke resources and claims. The previously rejected historical Linux cleanup remains an explicit manual overall-goal dependency; no alternate deletion is attempted.

## Results at the bridge head

All runs used commit `f3c7a4d1121aed6eff84bf32ed09264ba1ff02cc` in an owned, clean native checkout, one sequence at a time. The SDK was 10.0.303, owned by this task.

- Focused BookModal family: 18 of 18 passed, with no skips. Both same-route account-change and sign-out cases now pass, and so do the initial prerender case and every earlier lifecycle and route case.
- Full local gate (`docs/Test.md#local-gate`): every listed command passed, including `git diff --check`, restore, all CI Python suites, workflow policy, `dotnet format --verify-no-changes`, release/cloud/observability validation, the deployment audit, the doc-slice loader, rendered templates, yamllint, shellcheck and actionlint. `validate-frontend-profiles.sh` produced:

  | Profile | Non-account tests | Account tests |
  | --- | --- | --- |
  | BlazorAuto | 188 passed, 16 expected opt-in skips, 204 total | 171 of 171 |
  | React | 180 passed, 16 expected opt-in skips, 196 total | 157 of 157 |

- Isolated final image: the `final-blazor` target was built from this commit and started with the CI smoke harness. A scratch copy of that harness, kept outside Git, enabled the Development-style seeded local user and added the Books journeys to its browser filter. Six of six browser cases passed with no skips: rapid modal navigation and focus, create/view/edit/cancel/back, the seeded-user bookcase CRUD across refresh and navigation, mobile author books, pre-hydration save state and render mode. Run-labelled containers were removed by the harness's own cleanup, and the image was deleted afterwards.
- The bridge adds no CSS class, so Tailwind output is unchanged.

## Rebase onto the audit fix

Exact-head CI 41365398917 at the first push failed only `npm audit`. The cause was the new minimatch advisory GHSA-g3hv-75wg-m249, which this change does not touch. [PR 409](https://github.com/Grumlebob/GrumPlates/pull/409) fixed the audit and was integrated as `b75344e0`. This branch was then rebased onto it without conflicts. At the rebased code head `28e49c224990f53078af7b35ebeaf5f6592e03d8`, the same full local gate passed with identical profile results. The same isolated final-image run passed 6 of 6 Books/app-shell browser cases with no skips, and its image was removed.
