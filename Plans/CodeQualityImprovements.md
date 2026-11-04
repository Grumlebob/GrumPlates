# Code quality improvement backlog

This backlog starts from the accepted SonarQube baseline at `8393173bc4f580ae1dc036a533e4e47281099add`. It records reviewed follow-up work; it does not authorize implementation. Profile-specific issue keys are kept together when they represent the same source finding. Shared findings are deduplicated for planning, and profile counts are not combined.

## Prioritized work

### Q-002 — Replace the MD5-based packaged Caddyfile check with a package-aware verification

- Evidence: `python:S4790` keys `6917f011-a7e9-4a61-ab3d-bede2d9924b0` (BlazorAuto) and `9c847807-d7f0-415f-a177-d0ac3a19699d` (React) at `Deployment/LocalSingleNode/Scripts/lib/collisions.py:21`.
- Why it matters: this privileged preflight decides whether a Caddyfile is the unmodified packaged default before allowing setup to proceed. The current code hashes the file with MD5 and compares it with dpkg's recorded conffile checksum. That is not password hashing or a cryptographic signature, but collision resistance is still relevant to a fail-closed ownership decision.
- Proposed change: check whether dpkg exposes a stronger supported verification path or whether the exact packaged default can be compared directly. If the package metadata only supplies MD5, document that constraint and keep foreign or ambiguous files rejected.
- Behavior to preserve: accept the repository-owned Caddyfile and a verified pristine package default; reject symlinks, edited files, and foreign configuration before any host changes.
- Verification: extend the Caddy collision fixtures for pristine, changed, foreign, and ambiguous files; run the LocalSingleNode setup tests and deployment audit.
- Effort: medium; the check runs before privileged host adoption and must remain fail-closed.
- Dependencies: none.
- Decision: fix through a bounded design review; do not replace the check with an unconditional allow or a weaker comparison.
- Acceptance: modified or foreign Caddy configuration never passes as a packaged default, and the verification basis is explicit.

### Q-003 — Add focused coverage for BlazorAuto authentication-management routes

- Evidence: BlazorAuto coverage report at the baseline SHA shows 0.0% for `BlazorAutoApp/Features/Login/Account/Pages/ExternalLogin.razor`, `.../Manage/Passkeys.razor`, `.../Manage/TwoFactorAuthentication.razor`, and `.../Manage/EnableAuthenticator.razor`. This is coverage evidence, not a Sonar issue key. An E2E test already visits the Passkeys route, so zero imported line coverage does not mean the route has no test at all.
- Why it matters: external-provider sign-in, passkey management, and two-factor setup are authentication-sensitive flows whose success, cancellation, and error behavior deserve direct verification.
- Proposed change: add focused component or browser tests for route entry and the relevant success, invalid-input, cancellation, and failure states in those pages. Reuse existing identity test support; do not target a percentage by excluding code.
- Behavior to preserve: current identity-provider redirects, passkey ownership checks, authenticator enrollment and reset behavior, recovery codes, and authorization boundaries.
- Verification: tests assert visible state and server-side outcomes for each changed flow; run both frontend profiles and the existing identity E2E suite where applicable.
- Effort: medium; the pages rely on authentication and browser navigation behavior.
- Dependencies: none.
- Decision: fix in a future application-quality wave.
- Acceptance: meaningful success and failure paths have behavioral tests, with coverage evidence mapped to the actual page code.

### Q-004 — Test the route-driven book modal's asynchronous and destructive paths

- Evidence: BlazorAuto risk-ranked file `BlazorAutoApp.Client/Features/Books/BookModal/BookModalHost.razor` has 443 ncloc, risk score 57.0, and 31.9% line coverage. This is file-level coverage evidence, not an issue key.
- Why it matters: the component coordinates route changes, cancellable loads, save/delete operations, confirmation, and focus behavior. Low coverage leaves those state transitions less visible during later UI changes.
- Proposed change: add focused tests for stale-load cancellation, create/edit route changes, save errors, delete confirmation/cancel, and keyboard/focus handling. Keep pure formatting and model behavior in existing unit tests.
- Behavior to preserve: current URLs, author-book versus user-book permissions, cancellation semantics, and modal accessibility behavior.
- Verification: tests assert externally visible route, modal, and operation outcomes; run BlazorAuto browser regression and both profile gates for any code change.
- Effort: medium; asynchronous route transitions and browser focus need controlled test support.
- Dependencies: none.
- Decision: fix in a future application-quality wave.
- Acceptance: high-risk modal transitions have behavior-focused tests and the component's measured coverage improves without excluding production lines.

## Reviewed findings with no immediate follow-up

| Finding | Review and decision |
|---|---|
| `python:S3516` at `Deployment/LocalSingleNode/Scripts/lib/doctor.py:46` (keys `2ae477d5-b59b-4429-88c5-0a2a1223e67a` / `54eb0af8-565a-42d6-850b-dc6f18194ce4`) | The method builds and returns a list of named checks whose boolean results depend on live host state. It does not always return the same value. Reviewed noise; no code change. |
| `python:S5332` at `Deployment/LocalSingleNode/Scripts/lib/collisions.py:85` (keys `439e80ec-6d02-4fda-b363-3879b4e68a8b` / `5bb47505-d963-4089-b136-65c7b77d5cdb`) | The `http://` literal is matched inside Caddy configuration text to detect a port-80 collision; this code does not issue an HTTP request. Reviewed noise; keep the static collision check. |
| `csharpsquid:S2068` on `User123` and `postgres` defaults (for example `7a82f2b5-f3eb-47a3-8811-cb53cbb32f21`, `8414802e-46f7-4940-9ca0-fab2d83c054f`, `6cc2cb39-bcb1-4397-82ee-0b7406a8ae68`, `85c584f0-a498-4a73-ac6d-2d91a009efd0`) | These historical findings pointed to local/demo credentials and sample connection strings. The simulator defaults were removed with P16; Docker local account seeding is controlled by `LocalAccounts:Enabled`, `.env.example` documents local PostgreSQL defaults, and deployment compositions inject connection strings from protected settings. No report finding identified a live secret. Keep production secrets injected. |
| `csharpsquid:S2245` at the retired simulator ScenarioRunner.cs:29` (keys `5e42d43c-bc21-4706-8e18-ac320bf64c56` / `5fbb4709-60cd-4a38-ae00-4de542e24da4`) | Historical finding only. P16 removed the deterministic synthetic scenario selector with the standalone simulator; no cryptographic use was involved. |
| CA1861 in `BookCoverGeometryTests.cs:23` and `UserBookcaseStateTests.cs:41/61/84` | Four per profile. Literal arrays feed one-off assertions; static fields add shared mutable test state without a useful allocation benefit. Retain test-only suggestions. |
| csharpsquid:S6966 at `UserBookcaseStateTests.cs:56` | The test cancels a fresh token source before passing the token to the stub API; no callbacks are registered. Synchronous Cancel deliberately creates an already-canceled token, so CancelAsync adds no useful behavior. |
| csharpsquid:S6966 at BlazorAutoApp/Program.cs:71 | The analyzer prefers awaiting app.RunAsync. Keep the conventional synchronous ASP.NET Core host lifetime call in this cleanup; it is not dead code or a new regression. Revisit only with a dedicated async startup/lifetime change. |
| `javascript:S8786` at `BlazorAutoApp.React/scripts/openapi-contract.mjs:41` (key `34d0470d-6ef0-42d6-9b23-f7bd1fb49fa4`) | The expression `\n*` normalizes trailing line feeds in generated text; it is a single-character repetition rather than a nested or ambiguous backtracking pattern. Reviewed noise; no performance defect was observed. |

The SonarQube quality gate remains informational. Implement these items only through separately authorized, tested changes; do not suppress rules or exclude source just to reduce counts.
