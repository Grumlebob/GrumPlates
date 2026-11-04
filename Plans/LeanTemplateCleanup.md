# Lean template cleanup

Status: **Complete** (2026-11-04). P00–P15 and D01–D10 were done first. The disk packets P16–P25 and D11–D16 fix image, payload, cache and journal retention in the template and downstream, and removed the rename-era leftovers once. All three live targets run `32dbde08`. The owner report is in the P20 ledger row. Goal: [LeanTemplateCleanupGoal.md](LeanTemplateCleanupGoal.md).

This is an execution runbook for an agent that has no memory of the conversation that produced it. Do one packet at a time, in the order in section 5. Record evidence as section 3 describes. If a packet's checks fail, stop that packet and fix it or report. Do not skip ahead.

## 1. Goal

Make GrumPlates a lean template:

1. **No legacy states.** Remove code, settings, workflows, tests and docs that exist only to migrate, import, pin, lock or tolerate state from earlier versions of this repository or its deployments. A fresh clone never has such state.
2. **One account model.** Accounts are always on and always use the hardened account code. Remove the `Accounts:Enabled` switch, the original-scaffold Blazor Identity behaviour it kept alive, and the separate `LocalAccounts` demo seed.
3. **Fewer brittle parts.** Remove settings and checks that duplicate each other, pinned values that must be edited in many places, and checks that read prose documentation.
4. **Total repository hygiene.** Remove stale branches, resolve open PRs, delete finished plans, and remove local build leftovers.
5. **Cheap downstream wins.** Unblock the failing Dependabot PRs in CarbonCalculator, ImprovedDb and Portfolio, and port the small template fixes that apply there, without major refactoring (section 7).

Keep everything that represents the intended style for new code: vertical feature slices, caching and cache invalidation, rate limiting, observability, health checks, the three deployment targets, digest-pinned releases, migrations provenance, scoped maintenance and backups.

## 2. Owner decisions (2026-11-02)

| ID | Decision |
| --- | --- |
| OD-1 | The project must be lean. Do not support legacy states. |
| OD-2 | Blazor (`BlazorAuto`) and React are both supported frontend choices. A clone selects one through `frontend-profile.txt`. Neither is legacy. Never remove or weaken either profile. |
| OD-3 | Accounts are always on. |
| OD-4 | Total cleanup: stale branches removed, merged or fixed; open PRs resolved. |
| OD-5 | The standalone simulator (`.Simulation`) is already removed (PR #206). Nothing remains in Git; only ignored local build output still mentions it (packet P13). |
| OD-6 | No local mail server. Local development and Blazor E2E use **seeded accounts**, not self-registration. There is one hardened seed mechanism (packet P03-C). Registration E2E is not required; registration stays covered by integration tests with the existing SMTP test fixture. |
| OD-7 | Squash the four EF migrations into one initial migration (packet P07). |
| OD-8 | Remove the never-enabled CI runner profile routing; keep the fixed runner path (packet P05). |
| OD-9 | Remove deployment-audit rules that read Markdown docs (packet P11). |

Recommendations from the 2026-11-02 review. They are accepted when the owner runs the goal file; do not ask about them again.

| ID | Decision | Why |
| --- | --- | --- |
| OD-10 | In the `Development` environment on a non-Windows OS, when no key-encryption certificate is configured, store Data Protection keys in PostgreSQL **unencrypted** and log one warning at startup. Every other case keeps today's enabled behaviour: DPAPI on Windows Development; otherwise a certificate is required, and startup fails without one. | `dotnet run` on Linux/macOS and `Scripts/Testing/RunReactPublicE2E.ps1` (CI, Linux, `Development`, no certificate) would otherwise fail at startup. This is ASP.NET Core's own default on Linux. |
| OD-11 | `GET /api/client-config` keeps `schemaVersion: 1` and only drops `accountsEnabled`. | The server and both clients ship in one image, so an old client never talks to a new server. A version bump would only add edits. |
| OD-12 | `docker/setup-local.ps1` fills an **unconfigured** seed with generated values, in a new or an existing `.env`. It never changes a seed that already has an email or password. | Existing developer `.env` files already hold empty `Accounts__AdminSeed__*` lines from `.env.example`, so "new `.env` only" would leave them without accounts. |
| OD-13 | Bring the LocalCluster database up to date (deploy current `main` with migrations) **before** the squash (packet P01-B). | LocalCluster lacks migration `20261015183858_AccountOutboxAndDataProtectionKeys` (section 4.2). It must have all four old migrations before its history can be rewritten in P14. |
| OD-14 | P07 is one PR: rename the `ApplicationUser` namespace and squash. | The regenerated snapshot carries the new namespace, so the old migration files never need editing. |
| OD-15 | P05 deletes `select-ci-runner.yml`. Jobs use `runs-on: [self-hosted, linux, x64, "${{ vars.CI_RUNNER_LABEL }}"]`. | The reusable workflow only passes a variable through once routing is gone. |
| OD-16 | Downstream work is limited to packets D01-D10: Dependabot unblocks, the Lighthouse override, one test fix, single Node source, settings tidy, and removal of the unused legacy key import and legacy release path. No downstream deploys or host changes. | The owner asked for benefits without major refactoring. Each item is small, already proven in the template, or unblocks red PRs. |
| OD-17 | Portfolio stays on Node 24 LTS. Close #197 and ignore Node major updates in its Dependabot config; adopt the single Node source so patch updates are one edit. | Its `docs/Frontend.md` documents Node 24 as a deliberate choice; a major runtime move is a refactor decision for the owner. |
| OD-18 | Not ported: runner-routing removal, migration squash, `refresh_rating_ranks`, the accounts-switch removal and `LocalAccounts` replacement, ImprovedDb's release path, Markdown audit rules (section 7.3). | They are in use downstream, risk live data, or are major refactors. |

Earlier decisions that still hold: Identity (with MFA, passkeys, external login) stays; Cloud, LocalCluster and LocalSingleNode stay as supported template targets; do not add "absence" tests that prove a removed thing is gone (an earlier owner rule, recorded in `Plans/TemplateCleanupAndFeatureRetirement.md` section 10, which packet P12 deletes).

## 3. Rules for the executor

Read [AGENTS.md](../AGENTS.md) and [docs/Test.md](../docs/Test.md) completely before starting. They override anything here that conflicts. The most important rules:

- **One PR per packet** (P03 has five sub-PRs). Branch from fresh `origin/main`. Name branches `cleanup/lean-<packet>-<yyyymmdd>`, for example `cleanup/lean-p02-20261103`.
- **Evidence and ledger.** Put the packet's evidence (template at the end of section 12) in its PR description before merge. Do not push a separate PR just to update the ledger. Instead, the **next** packet's PR also changes that packet's ledger row to `Complete`, with its PR URL, merge SHA and exact-main CI run. P15 records the last ones. GitHub-only and host-only packets (P00, P01-A, P01-B, P13, P14) and the downstream packets (D01-D10, whose evidence also goes in their own downstream PR descriptions) get their ledger rows updated in the next GrumPlates PR the same way.
- **Merge command:** `gh pr merge <n> -R Grumlebob/GrumPlates --squash --delete-branch`. Then `git fetch --prune` and delete the local branch.
- **Code/config PRs:** run the full local gate in [docs/Test.md](../docs/Test.md#local-gate) before pushing. Merge only when `build-test-push` is green on the **exact PR head**. After merge, wait for green CI on the exact merge commit on `main` before marking the packet complete.
- **Docs-only PRs** (every changed file is `.md`/`.mdx`): follow the AGENTS.md exception: `[skip ci]` in commit and squash title, `git diff --check`. Anything under `Deployment/` or `docs/` still requires `bash Deployment/LocalCluster/Scripts/audit-deployment.sh` before merge, because the deployment audit reads Markdown (until packet P11 changes that).
- **Deleting an `.html`, `.svg`, `.json` or script file is not docs-only.**
- Stage explicit paths. Never `git add -A` or `git add .`.
- Keep `dotnet build` and `dotnet test` sequential in one checkout.
- When a change alters behaviour the deployment audit (`Deployment/LocalCluster/Scripts/Component/lib/audit_deployment.py`) checks, update the audit **in the same PR**: replace the old rule with a rule for the new behaviour. Exception: when the behaviour is removed entirely (a legacy path), remove its rule. Do not add a rule that asserts the legacy thing is absent (OD-1 / no absence tests).
- Do not pin exact test counts anywhere. If a test or script counts tests, remove the coupling rather than bumping a number.
- Rebuild and commit `GrumPlates/wwwroot/tailwind.css` when Razor classes or `GrumPlates.Client/Styles` change (`cd GrumPlates.Client && npm ci && npm run css:build`).
- **Never dispatch** `CD - Deploy LocalCluster`, `CD - Deploy LocalSingleNode` or `CD - Cloud` without the operator's explicit authorisation. The goal file authorises exactly the dispatches, backups and history rewrites in P01-B and P14, and nothing else. Never dispatch Cloud.
- **Never** run `docker volume prune`, `docker system prune`, or pass `--volumes` to Compose down on a node. Unused volumes left behind by this cleanup stay on the hosts.
- Do not touch `Deployment/Cloud/infra/opentofu/terraform.tfstate*`, `*.tfvars` or `.terraform/`. They are the owner's real, ignored Cloud state.
- Line numbers in this plan refer to `d28a6c7b`. Always re-find by symbol name with `git grep -n` after rebasing.

### 3.1 How to find every consumer before deleting

For each symbol, setting or file you remove, search tracked files for **all** of these spellings and fix or delete every hit:

```bash
git grep -n -i '<Name>'                 # C#, Razor, Python, YAML, Markdown
git grep -n '<Section>:<Key>'           # .NET configuration (colon form)
git grep -n '<Section>__<Key>'          # environment-variable form
git grep -n '<snake_case_name>'         # Ansible / Python settings form
```

Search `GrumPlates*`, `Deployment`, `Scripts`, `.github`, `docker`, `docker-compose.yml`, `.env.example`, `docs`, `README.md` and `AGENTS.md`. Ignore `Plans/` (deleted in P12) and `GrumPlates/Infrastructure/Persistence/Migrations/` (never edit migration `Up`/`Down` bodies).

### 3.2 Which tests to run for a packet

- C#/Razor: `dotnet build GrumPlates.sln -c Release` then `dotnet test GrumPlates.Test/GrumPlates.Test.csproj -c Release --no-build`. Then `bash Scripts/CI/validate-frontend-profiles.sh /tmp/frontend-profile-results`, which builds and tests **both** frontend profiles. Both must pass.
- React: from `GrumPlates.React`: `npm ci`, `npm run lint`, `npm test`, `npm run build`. Read `package.json` `scripts` for the exact names, including the OpenAPI contract check.
- Python deployment/CI scripts: the `python3 -m unittest ...` lines in `.github/workflows/ci.yml` for the touched directories.
- Shell: `shellcheck --severity=warning <files>`.
- Workflows: `bash Scripts/CI/run-actionlint.sh` and `python3 Scripts/CI/workflow_policy.py --repo-root .`.
- Always finish with the full local gate before pushing.

The full gate runs on WSL. On the owner's PC use `repo-gate <branch>` from Git Bash after committing (see the owner's machine notes); it takes about 15 minutes, so run it in the background.

## 4. Facts verified on 2026-11-02 (pre-work)

Use these as starting evidence. Re-verify any fact a packet depends on, because time will have passed.

### 4.1 Repository and branches

- `origin/main` = `d28a6c7b3afb0a406a217a5508e103de6227e2f8`. Primary checkout clean; one worktree (the primary).
- Tracked size: about 154k lines. Deployment ≈ 55k, Scripts ≈ 15.6k, Plans ≈ 15.6k (51 files), app projects ≈ 22k, tests ≈ 14k.
- Local + remote branch `coord/node-inventory-handoff` at `c6a59ba3afe8afe4a041400cd088ae9c43dbf4ba` (2026-10-11). Three docs commits adding `debug.md`: an obsolete coordination note for setting up the React demo, which is now live. No PR was ever opened. **Delete; do not merge.**
- Remote `improveddb` points to the ImprovedDb repository and is used for comparisons. Its branches are not ours. **Keep the remote.**
- Open PRs (all Dependabot, all failing `validate`):

| PR | Change | Failing step | Cause | Fix |
| --- | --- | --- | --- | --- |
| #405 | NuGet minor/patch (`Directory.Packages.props`) | Audit React frontend dependencies | Ran before the minimatch fix #409 landed | `@dependabot rebase` |
| #407 | lighthouse 13.5.0 → 13.6.0 (`GrumPlates.Client`) | Audit frontend dependencies | Lighthouse 13.6.0 brings `@sentry/bundler-plugins` → `glob` 13.0.6 → `minimatch` 10.2.6 (GHSA-g3hv-75wg-m249, fixed in 11.0.1). #409 only patched `GrumPlates.React`, so a rebase alone **does not** fix it (verified with `npm audit --package-lock-only` on the PR lockfile, 2026-11-02). | P01-A step 3a, then `@dependabot rebase` |
| #406 | Docker `node:26.12.1` → `26.13.0` (`GrumPlates/Dockerfile`) | Build and smoke both frontend image profiles | `npm ci` fails `EBADENGINE`: `GrumPlates.React/package.json` requires exactly `"node": "26.12.1"` | Packet P08, then rebase |

### 4.2 Live deployments

- GitHub variables: `DEPLOY_TARGETS=localcluster,localsinglenode`; `ADDITIONAL_FRONTEND_PROFILES=React`; `LOCALSINGLENODE_HOST=node-demo`; `CI_RUNNER_HOST=node-demo`; `CI_RUNNER_LABEL=localsinglenode-books`; `CI_RUNNER_ROUTING_ENABLED=false`. Environments: `cloud-hetzner`, `localcluster`, `localsinglenode`, `localsinglenode-react`.
- Cloud has **no live deployment** (owner, 2026-10-26). Keep the Cloud target's template files; do not touch Cloud state.
- Every tracked inventory sets `data_protection_legacy_key_store: None` and `data_protection_legacy_redis_key: ""`. The legacy Data Protection import is unused everywhere.
- Every tracked inventory sets `accounts_enabled: false`. Live apps currently run with accounts **off**, so they store Data Protection keys in Redis. Every target's `app.env.j2` already supplies `DataProtection__KeyEncryptionCertificatePath=/run/secrets/data-protection.pem` and its password. Turning accounts on needs no new deployment secret.
- The last deploys (LocalSingleNode on 2026-10-28, from `fe457e0599022001e4c0faab5fa1c2c5567eb894`; LocalCluster on 2026-10-28) used `RELEASE_PAYLOAD_TRANSPORT: ghcr-container`. They did not use the legacy Actions-artifact path. The last legacy-pin workflow run was 2026-10-16.
- Repository secrets: only `ANSIBLE_VAULT_PASSWORD` and `LOCALSINGLENODE_PUBLIC_TUNNEL_TOKEN`. The runner-profile secrets (`CI_RUNNER_PROFILES_JSON`, `CI_RUNNER_INVENTORY_APP_PRIVATE_KEY`) and the variable `CI_RUNNER_INVENTORY_APP_CLIENT_ID` **do not exist**. Profile routing has never been enabled.
- No branch protection and no rulesets on `main`. CI on `main` was green for `d28a6c7b`, `b75344e0` and `cbe751cd`.
- Host pin stores hold only `ghcr-` pin keys: `/opt/books/release-payloads/Grumlebob-GrumPlates/` and `/opt/books-react/release-payloads/Grumlebob-GrumPlates/` on `node-demo`, and `/opt/books/release-payloads/` on `node-main`. `pin_release_payload.py` (~712-718) rejects any other entry, so a stray `legacy-` pin would already have failed deploys.
- Deployed apps never migrate at startup (`Database__RunMigrationsAtStartup: "false"` in all three deployment compose files). Migrations run only through the release's migration bundle when a CD run has `run_migrations=true`. That path takes a pre-migration backup first (LocalCluster `site.yml` "Create pre-migration database backup"; LocalSingleNode `single_node_stack` "Create pre-migration custom-format backup under the existing deploy lock"). Local `docker-compose.yml` migrates at startup (`"true"`).
- Live databases (read-only checks, 2026-11-02). SSH aliases are in the owner's `~/.ssh/config`; every host has passwordless `sudo -n`.

| Target | Host | Container | Compose dir | `POSTGRES_DB` / `POSTGRES_USER` | `__EFMigrationsHistory` |
| --- | --- | --- | --- | --- | --- |
| LocalSingleNode primary (Blazor) | `node-demo` | `books-postgres-1` | `/opt/books` | `books` / `books` | all 4 |
| LocalSingleNode additional (React) | `node-demo` | `books-react-postgres-1` | `/opt/books-react` | `books_react` / `books_react` | all 4 |
| LocalCluster | `node-db` | `books-postgres-1` | `/opt/books` | `ship` / `ship` | **3**: missing `20261015183858_AccountOutboxAndDataProtectionKeys` (ProductVersion `10.0.8`) |

  The four old IDs: `20260525172002_InitialTemplateSchema`, `20260526083400_UserOwnedBooks`, `20260528104203_NormalizeBooksForAuthorBooks`, `20261015183858_AccountOutboxAndDataProtectionKeys`.
  The LocalCluster database and user are still called `ship`, a leftover from the ImprovedDb origin. Renaming a live database is out of scope; leave it.
  `node-demo` also runs CarbonCalculator, and `node-db` also runs ImprovedDb. **Never touch containers, volumes or databases whose names do not start with `books`.**

### 4.3 Downstream copies of plans

`Grumlebob/CarbonCalculator` and `Grumlebob/Portfolio` (local checkouts next to this repo) already hold their own, newer versions of every CarbonCalculator and PortfolioStudio plan and support file that exists here. The template copies differ from them; the template copies are the stale ones. `docs/StandaloneProduct.md` already tells clones to delete template plans.

### 4.4 Inventory: account switch (`Accounts:Enabled`)

Production uses (re-find with `git grep -n -E 'AccountOptions\.Enabled|accountsEnabled|Accounts:Enabled'`):

- `GrumPlates/Program.cs:106`: maps account endpoints only when enabled.
- `GrumPlates/Features/Accounts/AccountOptions.cs`: `Enabled` property; `EmailEnabled => Enabled && ...`.
- `GrumPlates/Features/Accounts/DependencyInjection.cs:65`: early return when disabled (skips the mail worker, password validator, cookie and Identity hardening).
- `GrumPlates/Features/Accounts/AccountApplicationBuilderExtensions.cs:28,75`.
- `GrumPlates/Features/Accounts/Security/AccountPrincipalGuard.cs:12`.
- `GrumPlates/Features/Accounts/Security/AccountOptionsValidator.cs:17,47,69`: rules that say "X requires accounts enabled".
- `GrumPlates/Features/Accounts/ClientConfiguration.cs:22`: `GET /api/client-config` returns `accountsEnabled`. The shared record is `GrumPlates.Core/Features/Accounts/Contracts/AccountContracts.cs:3` `ClientConfiguration(int SchemaVersion, bool AccountsEnabled, string ApplicationId)`.
- **Blazor client:** `GrumPlates.Client/Features/Books/BooksAntiforgeryHandler.cs:27-35` checks `SchemaVersion: 1` and skips its work when `!clientConfiguration.AccountsEnabled`.
- `GrumPlates/Features/Accounts/Identity/IdentityNoOpEmailSender.cs`, registered in `IdentityServiceCollectionExtensions.cs:35`. `Blazor/Pages/RegisterConfirmation.razor:67` uses it to show the confirmation link on screen. That is original-scaffold behaviour.
- `GrumPlates/Features/Accounts/Blazor/IdentityComponentsEndpointRouteBuilderExtensions.cs:24,25,60,129`: rate limiting and antiforgery on `/Account/*` only when enabled.
- Blazor pages with `AccountOptions.Enabled` branches (keep the **enabled** branch in each): `ConfirmEmail`, `ConfirmEmailChange`, `ExternalLogin` (also `bypassTwoFactor: !AccountOptions.Enabled`), `ForgotPassword`, `ForgotPasswordConfirmation`, `Login` (`lockoutOnFailure:`), `Manage/ChangePassword`, `Manage/DeletePersonalData`, `Manage/Email`, `Manage/PersonalData`, `Manage/SetPassword`, `Register`, `RegisterConfirmation`, `ResendEmailConfirmation`, `ResetPassword`. All are under `GrumPlates/Features/Accounts/Blazor/Pages/`. `Input.SetAccountPasswordPolicy(AccountOptions.Enabled)` appears in several of them.
- `GrumPlates/Features/Accounts/Blazor/AccountPasswordLengthAttribute.cs` and `AccountPasswordPolicyInputModel.cs`: the dual password policy.
- `GrumPlates/Infrastructure/Hosting/AppCachingExtensions.cs:19,23,73,91`: key storage is PostgreSQL + certificate when enabled; Redis, file system, Docker certificate or DPAPI when disabled.
- `GrumPlates/Infrastructure/Hosting/AppRateLimiting.cs:48,71`: the "legacy account write" bucket using `RateLimiting:Authentication` when disabled.

React: `GrumPlates.React/app/features/accounts/account-session-controller.ts:169,345` and many tests in that folder; generated `app/api/generated/schema.d.ts` and `api/openapi.json`.

Tests: `GrumPlates.Test/TestSupport/Integration/WebAppFactory.cs` does **not** set `Accounts:Enabled`, so most integration tests run with accounts **off** today. `TestSupport/Accounts/RealCookieAccountFixture.cs` turns them on and generates a key-encryption certificate (lines ~30-55). It is used by 15 test files; `WebAppFactory` by 27. Also: `Features/Accounts/AccountConfigurationTests.cs`, `AccountFixtureTests.cs`, `AccountPasswordTests.cs`, `BlazorBooksTransportTests.cs`, `Infrastructure/Hosting/RateLimitingTests.cs`, `Infrastructure/Hosting/ForwardedHeadersTests.cs`.

Deployment: `accounts_enabled` in all three inventories and example files, `Deployment/*/ansible/**/app.env.j2`, the Ansible `assert` tasks (`Deployment/{Cloud,LocalCluster}/ansible/roles/app/tasks/main.yml`, `Deployment/LocalSingleNode/ansible/roles/single_node_stack/tasks/main.yml`), `Deployment/Common/Scripts/Component/lib/account_deploy_settings.py`, `Deployment/LocalSingleNode/Scripts/lib/ls_settings.py`, both `validate-rendered-templates.sh`, `Deployment/LocalCluster/Scripts/ci-docker-smoke.sh` (`accounts_enabled` branch at ~222/283), audit rules at ~1911, 2007, 2989, `Scripts/Test-DeployedSite.ps1` (`AccountsEnabled` branches), `Scripts/CI/tests/test_deployed_site_acceptance.py`, `.env.example`, `Deployment/LocalCluster/Settings.md`, `Deployment/Cloud/Setup.md`, `docs/Accounts.md`, `docs/Requirements.md:38,40`.

Important consequence: with accounts on, **registration requires SMTP**. `AccountOptionsValidator` refuses `RegistrationEnabled` without an email host. Blazor E2E (`GrumPlates.Test/E2E/Support/BlazorE2ETestBase.cs` `RegisterUniqueUserAsync`, used by `BooksE2ETests` and `IdentityE2ETests`) registers through `/Account/Register` and currently relies on the old mode. Per OD-6 it switches to seeded accounts (packet P03-E). Blazor E2E is opt-in and local (`RUN_E2E=1`); CI does not run it. `GrumPlates.Test/E2E/Support/E2ETestCredentials.cs` holds only the shared `Password = "Passw0rd!"`.

Places that start the app without a key-encryption certificate today, and so break once accounts are always on:
- `Deployment/LocalCluster/Scripts/ci-docker-smoke.sh:181-195`: the first web container (`ASPNETCORE_ENVIRONMENT=Docker`, no certificate) for **both** profiles. Only the later React fixture container (~398-424) sets `DataProtection__KeyEncryptionCertificatePath=/run/react-auth-fixture/server.pfx`. Fixed in P03-B step 10.
- `Scripts/Testing/RunReactPublicE2E.ps1:229-240` (`ASPNETCORE_ENVIRONMENT=Development`, Linux CI runner, `ci.yml` ~483). Fixed by OD-10.
- Local `docker-compose.yml` is fine: it runs `Docker` with the Kestrel certificate `/https/aspnetapp.pfx`, which the key-encryption code already falls back to (`ASPNETCORE_Kestrel__Certificates__Default__Path`).
- Every deployment already mounts `/run/secrets/data-protection.pem` from the vault.

### 4.5 Inventory: `LocalAccounts` demo seed

- `GrumPlates/Features/Accounts/Blazor/Seed/LocalLoginAccountSeedExtensions.cs` (236 lines). It seeds `user@user.com / User123`, bypassing password validation. On every start it also locks the published `admin@admin.com / Admin123` and, in Docker, both published accounts.
- Called from `FrontendComposition.SeedFrontendDataAsync` (find with `git grep -n SeedLocalLoginAccountsAsync`).
- Config: `GrumPlates/appsettings.Development.json` (whole `LocalAccounts` section), `appsettings.Docker.json` (`LocalAccounts:Enabled=false`), `docker-compose.yml:39` (`LocalAccounts__Enabled: "true"`), the three deployment compose files (`LocalAccounts__Enabled: "false"`), `ci-docker-smoke.sh`, its test, `Deployment/LocalSingleNode/Scripts/validate-rendered-templates.sh`, audit rules (3 hits), `docs/Requirements.md`, `docs/HowToRunLocally.md:30`.
- Tests: `GrumPlates.Test/Features/Accounts/Blazor/LocalLoginAccountSeedTests.cs` (326 lines), plus `LocalAccounts` settings in `WebAppFactory.cs` (lines ~39, 97, 225) and `ForwardedHeadersTests.cs:69`.
- The separate `Accounts:AdminSeed` (`GrumPlates/Features/Accounts/AdminAccountSeed.cs`) is the hardened, create-once seed. **Keep it.**

### 4.6 Inventory: legacy Data Protection import

- `GrumPlates/Infrastructure/Hosting/DataProtectionKeyStartupExtensions.cs` (448 lines): `ImportAndValidateDataProtectionKeysAsync`. It imports keys from `LegacyKeyStore` (Redis or FileSystem) into PostgreSQL under an advisory lock, and re-wraps stored keys that are unencrypted. Both exist only for old state. **The whole file goes** (packet P02 step 3).
- `DataProtectionKeyStorageOptions` (bottom of `AppCachingExtensions.cs`): `LegacyKeyStore`, `LegacyRedisKey`, `LegacyKeyStoragePath`, `HasValidLegacySource()`.
- Tests: `GrumPlates.Test/Features/Accounts/DataProtectionKeyImportTests.cs` (827 lines).
- Deployment: `data_protection_legacy_key_store` / `data_protection_legacy_redis_key` in every inventory, `DataProtection__Legacy*` lines in every `app.env.j2`, `DATA_PROTECTION_LEGACY_HOST_PATH` in Cloud `compose.env.j2`, the `/run/legacy-data-protection` bind mount in `Deployment/Cloud/compose/app-server/docker-compose.yml:44-45`, the "Ensure legacy Data Protection import source directory" tasks in all three Ansible roles, the Ansible asserts, `Deployment/Common/Scripts/Component/lib/data_protection_deploy_settings.py` (`LEGACY_DATA_PROTECTION_SETTING_KEYS`, `validate_legacy_data_protection_settings`) and its callers (`cloud_settings.py:21-45,238`, `ls_settings.py:27-28,122-129`), `validate-rendered-templates.sh` (LocalCluster ~287-295, 477-483; LocalSingleNode ~14-67), `Deployment/Cloud/Setup.md:123`, tests in `Deployment/Common/Scripts/Tests/test_account_deploy_settings.py` and `Deployment/LocalSingleNode/Scripts/Tests/test_settings_fixture.py`.

### 4.7 Inventory: legacy release path (pre-GHCR Actions artifacts and manifest schema v1)

- Workflow `.github/workflows/pin-localsinglenode-legacy-release.yml` (137 lines).
- `Deployment/LocalSingleNode/Scripts/pin-legacy-release.py` + `Scripts/Tests/test_pin_legacy_release.py`.
- `Deployment/Common/Scripts/Component/lib/import_legacy_release_payload.py` (460 lines) + `Deployment/Common/Scripts/Tests/test_import_legacy_release_payload.py`.
- `pin_release_payload.py`: `LEGACY_PIN_KEY`, the `legacy-` alternative in `ANY_PIN_KEY`, `_legacy_identity`, `actions-artifact` transport, schema-v1 pins (~34-160).
- `release_manifest.py:42,91-98`: `allowed_schema_versions=(1, 2)` and the v1 branch.
- `release_status.py:23-24,62,84-85,118-120`: legacy pin key and the `legacy-artifact-only` status.
- `find-successful-ci-run.py:209-219,319`: the `legacy_artifact` outcome.
- `prune-actions-artifacts.py:257,329,445`: `legacy_dispatch_head_sha` and "zero retires a legacy family". Check whether the GHCR path still needs it before removing.
- Workflows: `cd-localsinglenode.yml` (~122-123 `--legacy-blazor-default`, ~168 pin-key regex, ~223-245 `legacy_artifact)` branch, ~309 v1 summary), `cd-localcluster.yml` (~295-296, ~349, ~372, ~427-449, ~543), `cd-cloud.yml` (~169-172), `ci.yml` (~289 and ~299 legacy test files), `localsinglenode-maintenance.yml` (the off-by-default "legacy diagnostic archive" input, see `Deployment/Common/ReleaseContract.md:19`).
- `Scripts/Frontend/resolve_frontend_profile.py:65-72`: `--legacy-blazor-default`.
- Ansible `single_node_stack/tasks/main.yml:21,24,197,349-352`: manifest v1 and `legacy-` pin keys.
- Tests that mention legacy: `test_pin_release_payload.py`, `test_release_status.py` (Common and LocalCluster), `test_registry_payload.py`, `test_prune_actions_artifacts.py`, `test_ci_provenance.py`, `test-verify-release-identity.sh` (both), `Scripts/CI/tests/test_frontend_profile.py`.
- Docs: `Deployment/Common/ReleaseContract.md`, `Deployment/LocalSingleNode/Operations.md`, `Deployment/LocalCluster/Operations.md`.

### 4.8 Inventory: CI runner selection

- `CI_RUNNER_ROUTING_ENABLED=false`, so CI uses the **fixed** runner path. The steps named "Verify legacy CI runner" (`ci.yml` ~84 and ~650) are the **active** path. **Do not remove them; rename them.**
- The unused, never-configured profile routing (PR #376, 2,289 lines): `.github/workflows/select-ci-runner.yml` (routing half), `Scripts/CI/runner_profiles.py`, `select_ci_runner.py`, `verify_ci_runner.py`, their tests `test_runner_profiles.py`, `test_select_ci_runner.py`, `test_verify_ci_runner.py`, `test_ci_runner_routing_workflow.py`, the "Verify selected CI runner profile" steps, the `pool` dispatch input, `docs/CI/RunnerProfiles.md`, `docs/HowToForkThisRepo.md` and `docs/Test.md` mentions, and audit rules from that PR. `Scripts/CI/require_ci_results.py` (the result gate) came in the same PR. **Keep the result gate.**
- Label fallback chain `vars.CI_RUNNER_LABEL || vars.LOCALCLUSTER_RUNNER_LABEL || 'localcluster-books'` (`ci.yml` ~42, and the same in the publication selector).

### 4.9 Inventory: other one-time or legacy code

- `Deployment/LocalCluster/Scripts/prepare-renamed-localcluster-app.sh` (277 lines) and `cleanup-renamed-localcluster-app.sh` (299 lines), referenced from `Deployment/LocalCluster/Operations.md`.
- `ci.yml` step "Test Caddy repository migration safeguards" (`Deployment/Common/Scripts/Tests/test_caddy_install.py`). Read the test and the Caddy install code: remove only migration-from-old-repository logic and keep the install.
- `Deployment/Cloud/ansible/roles/docker/tasks/main.yml:30` "Remove legacy Docker apt repository list".
- `Deployment/Cloud/Scripts/set-temporary-ssh-firewall.sh:147-173`: matching of old-style temporary firewalls (`legacy_role_match`, `legacy_name_match`).
- `GrumPlates.Test/Architecture/Boundaries/NoInfrastructureNamespaceTests.cs:64-76` `ServerProject_HasNo_Legacy_Root_Infrastructure_Folders`: an absence test.
- `GrumPlates.Test/Architecture/Slices/AccountsBoundaryArchitectureTests.cs:8-28`: allows `ApplicationUser` in the old `GrumPlates.Features.Login.Account` namespace.
- `ApplicationUser` lives in `GrumPlates/Features/Accounts/Identity/ApplicationUser.cs` but keeps the namespace `GrumPlates.Features.Login.Account` (176 references in about 40 files, including the migration designer files and snapshot).
- `GrumPlates.React/scripts/patch-redocly-minimatch.mjs` (#409): a current workaround for an upstream bug, **not** legacy. Keep it.
- `cd-localcluster.yml` input `refresh_rating_ranks` (an ImprovedDb leftover; this app has no rating ranks). It only rejects `true`. It also appears in `Deployment/Common/Scripts/Tests/test_release_status.py:34`.
- `audit_deployment.py:1682` checks for the old workflow name "Deploy Ship To LAN" (an old-name absence rule).

### 4.10 Inventory: brittle and duplicate items

- **Exact Node pin in nine places:** `GrumPlates/Dockerfile:3`, `GrumPlates.React/package.json:8` (`engines.node`), `.github/workflows/ci.yml:379,434`, `audit_deployment.py:985`, `Deployment/LocalCluster/Scripts/ci-docker-smoke.sh:44`, `Deployment/LocalCluster/Scripts/Tests/test-ci-docker-smoke.sh:272`, `Deployment/LocalCluster/Scripts/validate-rendered-templates.sh:168`, `Deployment/LocalSingleNode/Scripts/validate-rendered-templates.sh:17`, `Scripts/Testing/RunReactPublicE2E.ps1:135-151`. This is why #406 fails.
- **`GrumPlates/settings.defaults.json`** duplicates 15 of 16 shared keys in `appsettings.json`. It differs only in `Redis:Configuration` and adds the `Database:*` placeholders. It is loaded by `Infrastructure/Hosting/AppConfigurationExtensions.cs` and listed in `GrumPlates.csproj:37`.
- **`AppConfigurationExtensions` re-adds sources** that `WebApplication.CreateBuilder` already adds (`appsettings.json`, `appsettings.{Environment}.json`, environment variables). It also adds `appsettings.Docker.json` a second time when the environment is `Docker`.
- **`.env.example`** sets the same OpenTelemetry values twice (`Observability__OpenTelemetry__*` for the web container, `OBSERVABILITY_*` for Compose interpolation and `Scripts/RunLocal.ps1`). Its comment "Later observability phases enable this after a local Alloy collector exists" is stale: Alloy exists in `docker/observability/alloy`.
- **`Program.cs:26-47`** holds the account ProblemDetails code mapping inline. It belongs to the Accounts feature.
- **`DataProtectionKeyStorageOptions`** is declared at the bottom of `AppCachingExtensions.cs`.
- **`audit_deployment.py`** (3,164 lines) reads Markdown through `Deployment/LocalCluster/Scripts/Component/lib/doc_slice_loader.py` (13 references). A docs-only edit can break CI.
- **`ci.yml`** is 1,036 lines with about 30 separate `python3 -m unittest <file>` steps.
- **Three observability docs:** `docs/ObservabilityGuide.md`, `docs/Observability/Architecture.md`, `docs/Observability/Operations.md`.
- **`Plans/README.md`** says "Older retained plans, if any, live in `docs/plans/`". That folder does not exist.

### 4.11 Plans status

Keep: `Plans/README.md`, this file, `Plans/LeanTemplateCleanupGoal.md`, `Plans/CodeQualityImprovements.md` (open Q-002, Q-003, Q-004) and `Plans/CloudflaredConnectorFollowUps.md` (open steps 1-5). Every other file under `Plans/` is complete, superseded, or a stale copy of a downstream plan: **delete it** in P12.

### 4.12 Facts: node disk usage (2026-11-04, read-only)

Measured after P14 with `df`, `du`, `docker system df` and `docker buildx du`. Every node has one 234 GB root disk; Docker's image store is containerd (`/var/lib/containerd`).

| Node | Used | Free | Images (reclaimable) | Build cache (reclaimable) | Largest other use |
| --- | --- | --- | --- | --- | --- |
| node-main | 90% | 23 GB | 47 GB (38 GB) | 26 GB (18 GB) | `/opt/improveddb` 88 GB (`LocalData` 66 GB, `maintenance-volume-archives` 16 GB, `staged-operations` 4.7 GB); `/home/deploy` 11 GB (`.nuget` 7.1 GB); journals 2.4 GB |
| node-demo | 45% | 124 GB | 26 GB (21 GB) | 43 GB (43 GB) | `/home/deploy` 14 GB; journals 1.8 GB |
| portfolio-node | 30% | 157 GB | 4.8 GB (3.3 GB) | 41 GB (38 GB) | `/home/deploy` 8.1 GB |
| node-app1, node-app2 | 16% | 188 GB | 6.0 GB (1.5 GB) | 0 | `/opt/improveddb` 15 GB; journals 1.7 GB |
| node-db | 10% | 201 GB | 2.3 GB (1.0 GB) | 0 | — |

Only node-main is short of space. The LocalCluster CD requires 20480 MiB free on `/opt` (`check-node-main-capacity.sh`), and on 2026-11-04 it stopped there (Q19). Most of node-main belongs to ImprovedDb (its data, 78 app images and 100 release-payload images). Books uses about 3 GB there.

Causes the template owns:

1. **Old app images are never removed when Docker lists a digest as a tag.** `Deployment/LocalSingleNode/Scripts/lib/maintenance.py` `candidates()` reports any image with more than one `RepoTags` entry and never removes it ("Report only: image has multiple tags"). Docker 29 with the containerd store lists the pulled digest (`ghcr.io/grumlebob/books@sha256:…`) in `RepoTags` next to the tag. On node-demo, 21 of 65 books and books-react images look like this, so the daily maintenance keeps them forever. node-demo holds 34 `books` and 31 `books-react` images; the live and recovery releases need four. CarbonCalculator and Portfolio copied the same line.
2. **Rename-era leftovers have no owner.** Images and CI containers built before the rename carry `Grumlebob/BlazorAutoAppTemplate` in `org.opencontainers.image.source` or `localcluster.ci.repository`. The maintenance code proves ownership with the current repository name, so it skips them. They include:
   - on node-demo, 16 books and books-react images from 2–3 weeks ago;
   - on node-main, the CI smoke stack of cancelled run 38004425567 (`books-ci-web`, `books-ci-redis`, `books-ci-postgres` `-38004425567-1`), still running after three weeks, and its image `books:a5bd0287…-38004425567-1`;
   - on node-main, 30 stopped Testcontainers containers (`localcluster.ci.owner=tests`).
3. **LocalCluster host cleanup only runs by hand.** In `localcluster-docker-maintenance.yml` the `prune` job runs only on `workflow_dispatch`, and only the artifact job is scheduled. The scoped helpers (`prune-actions-runner-residue.sh`, `prune-ci-residue.py`, `prune-docker-residue.sh` in its default owned-only mode) therefore never run on their own. ImprovedDb and Portfolio carry the same manual-only switch.
4. **The BuildKit budget is not met.** The daily `localsinglenode-maintenance` step `build_cache.py --keep-gb 20` reports, for example, "before 45.70 GB (27.83 GB reclaimable); after 38.28 GB" (run 41707505177). The cache stays about twice its budget, and node-demo's `daemon.json` `builder.gc` limit (30 GB) does not hold it either. The cache is shared with CarbonCalculator's CI on the same daemon.

Other items: unlimited journald on every node (0.6–2.4 GB, P21); ImprovedDb's images, archives and staged operations on node-main (D13); portfolio-node's 41 GB build cache (1300 entries) and 108 portfolio images, and manual-only Portfolio maintenance (D12); per-user NuGet caches shared by two runners on node-main (reported only).

## 5. Order and dependencies

| Packet | Content | Depends on | Change type |
| --- | --- | --- | --- |
| P00 | Start: fetch, record base | — | none |
| P01-A | Branch and Dependabot PR cleanup | P00 | GitHub only |
| P01-B | LocalCluster migration catch-up deploy (OD-13) | P00 | deployment (authorised by the goal) |
| P02 | Remove legacy Data Protection import | P00 | code |
| P03 | Accounts always on (sub-PRs A–E) | P02 | code |
| P04 | Remove legacy release path | P00 | code |
| P05 | CI runner: keep fixed path, remove unused routing | P00 | code |
| P06 | Remove other one-time migration code | P00 | code |
| P07 | Rename `ApplicationUser` namespace and squash migrations (one PR) | P01-B, P03 | code |
| P08 | Single source for the Node version; then fix #406 | P00 | code |
| P09 | Configuration and code tidy | P03 | code |
| P10 | Consolidate CI unittest steps | P04, P05, P06 | code |
| P11 | Stop the deployment audit from reading Markdown | P10 | code |
| P12 | Plans and docs cleanup | all code packets merged | docs (+ one HTML delete → not docs-only) |
| P13 | Local junk cleanup on the owner's PC | anytime | local only |
| P14 | Rewrite migration history, deploy and verify live targets | P02–P12 merged | deployment (authorised by the goal) |
| D01-D05, D10 | Downstream quick wins and branch hygiene | P01-A | downstream code / GitHub |
| D06 | CarbonCalculator Node single source | P08 | downstream code |
| D07 | Downstream settings tidy | P09 | downstream code |
| D08 | CarbonCalculator legacy key-import removal | P02 | downstream code |
| D09 | CarbonCalculator and Portfolio legacy release-path removal | P04 | downstream code |
| P15 | Closeout | everything | GitHub/local |
| P16 | Old app images: count real tags only | P15 | code |
| P17 | LocalCluster scoped cleanup runs daily | P15 | code |
| P18 | One-time removal of rename-era leftovers | P16 merged and verified | hosts (needs the goal's disk authorisation) |
| P19 | Keep the BuildKit cache within its budget | P16 | code |
| P21 | Bound system logs on every node | P15 | code + hosts |
| D11 | Port P16 to CarbonCalculator and Portfolio | P16 | downstream code |
| D12 | Automatic cleanup downstream | P17, P19 | downstream code |
| D13 | ImprovedDb node-main retention fixes | P16, P17 | downstream code + its own maintenance runs |
| P20 | Disk closeout and owner report | P16–P19, P21, D11–D13 | hosts read-only / docs |
| P22 | Remove old release payload images on node-main (added 2026-11-04, Q20) | P17 | code |
| P19b | Pass the BuildKit budget to Docker in bytes (added 2026-11-04, Q22) | P19 | code |
| P23 | Remove this repository's old untagged images on LocalSingleNode (added 2026-11-04) | P16 | code |
| P24 | Remove old app images that exist only as a digest on LocalCluster app servers (added 2026-11-04, Q25) | P17 | code |
| P25 | A payload image deleted by an earlier untag counts as removed (added 2026-11-04, Q27) | P23 | code |
| D14 | Port P23 to CarbonCalculator and Portfolio | P23 | downstream code |
| D15 | Portfolio: release payload image retention (Q25) | D14 | downstream code |
| D16 | Port P25 to Portfolio (Q27) | D15 | downstream code |

Execute in this exact order: P00, P01-A, P01-B, D01, D02, D03, D04, D05, D10, P02, P03-A … P03-E, P04, P05, P06, P07, P08, P09, P10, P11, P12, D06, D07, D08, D09, P13, P14, P15, then the disk packets P16, P17, P18, P19, P21, D11, D12, D13, P20. Merge each PR before branching the next from fresh `main`. P04 and P05 both edit `ci.yml` and the audit; P05 rebases on P04's removals.

**Main is never deployable between P07 and P14 without the history rewrite.** Do not dispatch any CD run with `run_migrations=true` after P07 merges until P14 step 3 has rewritten that target's history.

## 6. Packets

### P00 — Start

1. `git fetch --all --prune`. The primary checkout may hold this plan as untracked or modified files; that is expected. Do packet work in a worktree: `git worktree add ../GrumPlates-lean origin/main --detach`, then `git -C ../GrumPlates-lean switch -c cleanup/lean-<packet>-<date>` for each packet.
2. Record the current `origin/main` SHA for the evidence of the first PR.
3. Run `gh pr list -R Grumlebob/GrumPlates --state open` and `git branch -a`. Compare with section 4.1. A new non-Dependabot PR or branch belongs to someone. Do not touch it. Record it and continue with the other packets. A new Dependabot PR gets the P01-A treatment.
4. Read section 2. OD-6 to OD-15 are decided; do not ask them again.

### P01-A — Branch and Dependabot PR cleanup

1. Re-check `coord/node-inventory-handoff`: `git log --oneline main..origin/coord/node-inventory-handoff` must show exactly the three commits ending at `c6a59ba3afe8afe4a041400cd088ae9c43dbf4ba`, and `git diff --stat main...origin/coord/node-inventory-handoff` must show only `debug.md`. If it changed, stop and ask.
2. Delete it: `git push origin --delete coord/node-inventory-handoff`, then `git branch -D coord/node-inventory-handoff`. Record the full SHA in the ledger.
3a. Before rebasing #407, open a small PR on `main` (branch `cleanup/lean-p01-client-minimatch-<date>`): in `GrumPlates.Client/package.json` add `"glob": { "minimatch": "11.0.1" }` inside the existing `overrides` object (keep the `@tailwindcss/cli` entry), the same version #409 used. Run `npm install --package-lock-only` in `GrumPlates.Client`; then `npm ci`, `npm audit --audit-level=moderate` and `npm run css:build` must pass. The override is inert until Lighthouse 13.6.0 arrives. Full local gate, merge when green.
3. For PRs #405 and #407: comment `@dependabot rebase` on each (`gh pr comment <n> -R Grumlebob/GrumPlates --body "@dependabot rebase"`). Wait for the new CI run. `build-test-push` should now pass. When it is green on the exact head, the repository's Dependabot auto-merge workflow may merge it; if it does not, merge with `gh pr merge <n> -R Grumlebob/GrumPlates --squash --delete-branch`. If CI still fails, read the failing step's log (`gh run view <run> --log-failed`), record the cause in the ledger and fix it in a separate PR or leave it for the owner.
4. Leave #406 open until P08 is merged.
5. Do not touch the `improveddb` remote or its branches.

Done when: the coord branch is gone locally and on GitHub; #405 and #407 are merged or have a recorded, non-cleanup failure.

### P01-B — LocalCluster migration catch-up (OD-13)

Purpose: LocalCluster's database must have all four old migrations before P07 squashes them. Deploy the current, already published `main` with migrations. Accounts are still off at this point, so the app behaves exactly as now. Authorised by the goal file.

1. `target_sha` = the newest `main` commit whose `CI` push run is `success`: `gh run list -R Grumlebob/GrumPlates --workflow ci.yml --branch main --event push -L 5 --json headSha,conclusion`. Use the first `success` row.
2. Check that nothing else is deploying: `gh run list -R Grumlebob/GrumPlates --status in_progress` and `--status queued` show no CD or maintenance run. Avoid 03:00–05:00 UTC (scheduled maintenance and retention).
3. Dispatch: `gh workflow run cd-localcluster.yml -R Grumlebob/GrumPlates --ref main -f run_migrations=true -f target_sha=<sha>`. Leave every other input at its default. Find the run with `gh run list -R Grumlebob/GrumPlates --workflow cd-localcluster.yml -L 1` and wait with `gh run watch <id> -R Grumlebob/GrumPlates --exit-status`.
4. If it fails, read `gh run view <id> -R Grumlebob/GrumPlates --log-failed`, report, and stop P01-B. P02 to P06 and P08 may continue; P07 waits.
5. Verify read-only: `ssh node-db "sudo -n docker exec books-postgres-1 psql -U ship -d ship -At -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY 1'"` lists all four old IDs (section 4.2).
6. Record the run ID, the SHA and the four IDs as evidence.

### P02 — Remove the legacy Data Protection import

Behaviour after this packet: the app never imports keys from an old store. Stored keys may still be validated if that validation is independent of the import (step 3).

1. Read `GrumPlates/Infrastructure/Hosting/DataProtectionKeyStartupExtensions.cs` completely, plus `DataProtectionKeyImportTests.cs`.
2. In `DataProtectionKeyStorageOptions` (bottom of `AppCachingExtensions.cs`), delete `LegacyKeyStore`, `LegacyRedisKey`, `LegacyKeyStoragePath` and `HasValidLegacySource()`. In `AddAppCachingAndDataProtection`, delete the `.Validate(options => options.HasValidLegacySource(), ...)` line.
3. **Delete `GrumPlates/Infrastructure/Hosting/DataProtectionKeyStartupExtensions.cs` completely** and remove `await app.ImportAndValidateDataProtectionKeysAsync();` from `Program.cs`. Reason (decided in review): every method in it serves the import or re-wraps keys that an older or imported store left unencrypted (`ImportAndValidateAsync`, `CreateLegacyRepository`, `PrepareExistingRows`, `ProtectUnwrappedKey`, `RequiresWrapping`, the imported-key name helpers, `GetImportLockId`). A fresh install writes keys through `PersistKeysToDbContext` with the configured protector, and ASP.NET Core's key ring already validates what it reads. The unencrypted Development keys allowed by OD-10 must not be re-wrapped either.
4. Tests: delete `GrumPlates.Test/Features/Accounts/DataProtectionKeyImportTests.cs`. If it contains a test that proves a key written by the app is encrypted at rest with a certificate, move that one test into the existing Data Protection or account configuration tests; delete the rest.
5. Deployment, all three targets (section 4.6 lists the files):
   - Delete `data_protection_legacy_key_store` and `data_protection_legacy_redis_key` from `Deployment/Cloud/inventory/prod/group_vars/all.yml`, `all.example.yml`, `Deployment/LocalCluster/inventory/prod/group_vars/all.yml`, `Deployment/LocalSingleNode/inventory/group_vars/all.yml`, `Deployment/LocalSingleNode/machine.example.yml` (if present), and `ls_settings.py` defaults.
   - Delete the `DataProtection__Legacy*` lines from every `app.env.j2`, and `DATA_PROTECTION_LEGACY_HOST_PATH` from Cloud `compose.env.j2`.
   - Delete the legacy bind mount (`source: ${DATA_PROTECTION_LEGACY_HOST_PATH...}` / `target: /run/legacy-data-protection`) from `Deployment/Cloud/compose/app-server/docker-compose.yml`, and the same in other compose files if `git grep legacy-data-protection` finds them.
   - Delete the three "Ensure legacy Data Protection import source directory" Ansible tasks and the legacy lines in the Ansible `assert` blocks.
   - In `data_protection_deploy_settings.py`, delete `LEGACY_DATA_PROTECTION_SETTING_KEYS` and `validate_legacy_data_protection_settings`. If the module becomes empty, delete it and its imports in `cloud_settings.py` and `ls_settings.py`. Remove the matching tests.
   - In both `validate-rendered-templates.sh`, delete the legacy assertions and the "filesystem legacy" render cases. In the LocalSingleNode one, also remove `legacyKey` from the `node_script` (line ~15).
   - Delete matching audit rules in `audit_deployment.py` (`git grep -n -i 'legacy' Deployment/LocalCluster/Scripts/Component/lib/audit_deployment.py`, keep only rules about other topics).
6. Do **not** remove `/etc/<app>/data-protection/legacy` directories from live hosts. They are harmless, and removing them is a host change.
7. Docs: delete the legacy-import paragraph in `Deployment/Cloud/Setup.md` (~123) and any `LegacyKeyStore` mention in `docs/` or `Deployment/**/*.md`.
8. Verify: `git grep -n -i 'LegacyKeyStore\|LegacyRedisKey\|LegacyKeyStoragePath\|data_protection_legacy\|legacy-data-protection\|DATA_PROTECTION_LEGACY'` returns nothing outside `Plans/`. Run the full local gate.

### P03 — Accounts always on

Five PRs, merged in order A → E. Each must leave both frontend profiles green.

Target behaviour for the whole packet:
- Account services, middleware, endpoints and the Blazor hardened behaviour are always registered.
- Data Protection keys are always stored in PostgreSQL (`PersistKeysToDbContext<AppDbContext>`) and protected with DPAPI on Windows Development or with the configured certificate everywhere else. That is the current "enabled" behaviour, unchanged.
- `Accounts:RegistrationEnabled`, `Accounts:Email:*`, `Accounts:PublicOrigin` and `Accounts:AdminSeed:*` keep their meaning. Registration still requires email.
- `GET /api/client-config` no longer reports `accountsEnabled`, and keeps `schemaVersion: 1` (OD-11).
- Development on Linux/macOS without a certificate stores keys unencrypted with a warning (OD-10).

#### P03-A — Test infrastructure first

Do this first so the server change in P03-B does not break 27 test files at once.

1. Move certificate generation out of `RealCookieAccountFixture` into a shared helper, for example `GrumPlates.Test/TestSupport/Accounts/TestKeyEncryptionCertificate.cs`. It creates a self-signed RSA 2048 PFX in a temp folder, with a password, and deletes it on dispose.
2. Make `WebAppFactory` always supply `Accounts:Enabled=true` (temporary; P03-B step 14 deletes it once the switch is gone), `DataProtection:KeyEncryptionCertificatePath` and `DataProtection:KeyEncryptionCertificatePassword` from that helper, both in `ConfigureAppConfiguration` and in the environment-variable block used for the packaged-host path (lines ~210-235). Create one certificate per factory instance and dispose it with the factory. Keep the existing `ConfigurationOverrides` so a test can still override values.
3. Simplify `RealCookieAccountFixture` to use the helper and stop setting `Accounts:Enabled` itself.
4. Run the full .NET test suite for both profiles. Tests that asserted disabled-mode behaviour now fail. List them in the PR description, then delete or rewrite each:
   - A test that only proves "disabled hides X" → delete.
   - A test that proves something about Books, caching or hosting and happened to run disabled → keep and fix the expectation (for example, private no-store headers or account cookies now present).
   - `AccountConfigurationTests` cases that toggle `Accounts:Enabled` → keep only enabled cases.
   - `AccountFixtureTests` case with `Accounts:Enabled=false` → delete.
   - `AccountPasswordTests.BlazorFormPasswordAttribute_PreservesLegacyDisabledPolicy` → delete. Keep `DtoTransport_Allows128NonBmpCodePointsAndLegacyShortProof` only if its "short proof" means "a sign-in or current-password field accepts any length up to the maximum". That is correct behaviour; rename it without "Legacy".
5. Do not change production code in this PR except what the tests need to compile.

#### P03-B — Server: remove the switch

1. `AccountOptions`: delete `Enabled`. Change `EmailEnabled` to `!string.IsNullOrWhiteSpace(Email.Host)`.
2. `AccountOptionsValidator`: delete the rules at ~17, 47 and 69 that require accounts to be enabled. Keep the rules about email, sender, public origin, registration-needs-email, and admin-seed email/password validity.
3. `DependencyInjection.cs` (`AddAccountFeature`): remove the `if (!settings.Enabled) return services;` early return so everything below always runs.
4. `AccountApplicationBuilderExtensions.cs` (~28, 75) and `Security/AccountPrincipalGuard.cs:12`: remove the early returns.
5. `Program.cs`: remove the `if (...Enabled)` around `app.MapAccountEndpoints();` so it always runs.
6. `Blazor/IdentityComponentsEndpointRouteBuilderExtensions.cs`: always `RequireRateLimiting(AppRateLimiting.AccountWritePolicyName)`; delete the `if (!accountsEnabled) logout.DisableAntiforgery();` block; at ~129 keep only the enabled branch.
7. Every Blazor page in section 4.4: replace `@if (AccountOptions.Enabled) { A } else { B }` with `A`. Replace `if (AccountOptions.Enabled)` in code blocks the same way. `bypassTwoFactor: !AccountOptions.Enabled` becomes `bypassTwoFactor: false`. `lockoutOnFailure: AccountOptions.Enabled` becomes `lockoutOnFailure: true`. `requirePassword = AccountOptions.Enabled || ...` becomes `requirePassword = true`. `Input.SetAccountPasswordPolicy(AccountOptions.Enabled)` → apply the policy unconditionally; then simplify `AccountPasswordLengthAttribute` and `AccountPasswordPolicyInputModel` so they no longer carry a "disabled policy". If a page no longer uses the injected `AccountOptions` except for `EmailEnabled`, keep the injection; otherwise remove it.
8. `AppRateLimiting.cs`: delete `isLegacyAccountWrite`, `accountsEnabled`, `AuthenticationPolicyName` (if unused afterwards) and `IsAccountWriteRequest` (if unused). The global limiter always uses `"global"` and `configuredOptions.Global`. Delete `Authentication` from `AppRateLimitingOptions`, from `appsettings*.json` and from `settings.defaults.json` (P09 deletes that file anyway). Delete `RateLimiting__Authentication__*` from `ci-docker-smoke.sh` (~192, 431), `docker-compose.yml`, `docs/Testing/Browser.md:21`, deployment templates and audit rules, and `AuthenticationRateLimitPermitLimit` from `WebAppFactory`/`WebAppFactoryOptions` and `RateLimitingTests`.
9. `AppCachingExtensions.cs`: delete `accountsEnabled`; always `PersistKeysToDbContext<AppDbContext>()` and always call `ConfigureEnabledAccountKeyEncryption` (rename it `ConfigureKeyEncryption`). Delete the Redis key persistence, the file-system branch, `ResolveDataProtectionKeyStoragePath`, `ProtectKeysWithDockerCertificate` and the `.Validate(... KeyStoragePath ...)` line. Delete `KeyStoragePath` from `DataProtectionKeyStorageOptions` and move that class to its own file `Infrastructure/Hosting/DataProtectionKeyStorageOptions.cs`. Keep the class name. Delete `DataProtection:KeyStoragePath` from `appsettings.json`, `appsettings.Docker.json` and `settings.defaults.json`.
   Implement OD-10 inside `ConfigureKeyEncryption`, in this order:
   1. Windows and `Development` → `ProtectKeysWithDpapi()` (unchanged).
   2. Resolve the certificate path as today (`DataProtection:KeyEncryptionCertificatePath`, else `ASPNETCORE_Kestrel__Certificates__Default__Path`).
   3. **New:** no usable path, `Development`, and not Windows → do not call `ProtectKeysWith...`. Log once with `Log.Warning("Data Protection keys are stored unencrypted because no key-encryption certificate is configured. This is allowed only in Development.")` and return.
   4. Otherwise behave as today: load the certificate, or throw the existing exceptions.
   Add unit tests for case 3 (Development, non-Windows, no certificate → starts) and case 4 (`Docker` or `Production`, no certificate → throws). Use `OperatingSystem.IsWindows()` only through a small injectable check if the test cannot run on both OSes; otherwise mark the case with a skip condition for Windows.
10. `Deployment/LocalCluster/Scripts/ci-docker-smoke.sh`: the first web container (~181) must get a key-encryption certificate, for both profiles. Before that `docker run`, create a temporary directory with `mktemp -d "${RUNNER_TEMP:-${TMPDIR:-/tmp}}/dp-cert-${session}-XXXXXXXX"`. In it, generate a self-signed certificate (`openssl req -x509 -newkey rsa:2048 -nodes -sha256 -days 2 -subj "/CN=ci-data-protection" -keyout key.pem -out cert.pem`) and a PFX with a random password (`openssl rand -hex 32`, then `openssl pkcs12 -export`). `chmod 0444` the PFX. Mount the folder read-only at `/run/dp-cert`. Set `DataProtection__KeyEncryptionCertificatePath=/run/dp-cert/dp.pfx` and `DataProtection__KeyEncryptionCertificatePassword=<password>`. Remove the folder in the script's existing cleanup trap. Mirror the existing React fixture code (~336-373) and keep that fixture as it is. Update `Deployment/LocalCluster/Scripts/Tests/test-ci-docker-smoke.sh` if it asserts the `docker run` arguments.
11. Remove the unused `/app/Storage` key folder: delete `RUN mkdir -p /app/Storage ...` from `GrumPlates/Dockerfile` only if nothing else writes to `/app/Storage` (`git grep -n 'Storage'` in `GrumPlates/`). Delete the `app_storage:/app/Storage` mounts and `app_storage` volume declarations from `Deployment/{Cloud,LocalCluster}/compose/app-server/docker-compose.yml` and `Deployment/LocalSingleNode/compose/docker-compose.yml`, and `./data/storage:/app/Storage` from `docker-compose.yml`. Update the backup scripts if they reference `app_storage` (`git grep -n app_storage`). Existing volumes stay on hosts; never prune them.
12. Client configuration, first half. In this PR, `GrumPlates/Features/Accounts/ClientConfiguration.cs` returns `accountsEnabled: true` as a constant. The response shape does not change yet, so the React client, `Scripts/Test-DeployedSite.ps1` and `ci-docker-smoke.sh` stay green. P03-D step 1 removes the field from the server and every consumer in one PR.
   - `GrumPlates.Client/Features/Books/BooksAntiforgeryHandler.cs`: delete the `if (!clientConfiguration.AccountsEnabled) { return await base.SendAsync(...); }` block, so the handler always does its account work. Keep the `SchemaVersion: 1` check.
   - Delete `BlazorBooksTransportTests.cs` cases that exist only for `accountsEnabled: false`.
13. Original-scaffold email shortcut: delete the `else if (EmailSender is IdentityNoOpEmailSender)` branch in `Blazor/Pages/RegisterConfirmation.razor`, which shows the confirmation link on screen. Then `git grep -n IdentityNoOpEmailSender -- ':!Plans'`. If `AddAccountFeature` now always registers its own `IEmailSender<ApplicationUser>` (read `Features/Accounts/DependencyInjection.cs`), delete `IdentityNoOpEmailSender.cs` and its registration in `IdentityServiceCollectionExtensions.cs`. If the no-op sender is still the implementation when no email host is configured, keep it.
14. Tests: remove the temporary `Accounts:Enabled=true` that P03-A added to `WebAppFactory` (both blocks) and anything left in `RealCookieAccountFixture` or other tests (`git grep -n 'Accounts:Enabled\|Accounts__Enabled' GrumPlates.Test`).
15. Delete types and members that are now unreferenced. Check at least: `AccountPasswordLengthAttribute` and `AccountPasswordPolicyInputModel` members for the disabled policy, `AppRateLimitingOptions.Authentication`, `AppRateLimiting.AuthenticationPolicyName`, and any `using` that is no longer needed. `dotnet build -warnaserror` must stay clean.
16. Verify: `git grep -n -E 'AccountOptions\.Enabled|Accounts:Enabled|Accounts__Enabled' -- GrumPlates GrumPlates.Client GrumPlates.Core GrumPlates.Test` returns nothing. The remaining `AccountsEnabled`/`accountsEnabled` hits (client-config contract, React, scripts, deployment, docs) belong to P03-D. Full local gate.

#### P03-C — One hardened seed mechanism; remove the `LocalAccounts` demo seed

Target behaviour (OD-6): there is one seed mechanism, built on the existing hardened `AdminAccountSeed`. It can seed an **Admin** (`Accounts:AdminSeed`, unchanged contract, also used by deployments) and an ordinary **User** (`Accounts:UserSeed`, new, same shape: `Enabled`, `Email`, `Password`). Each seed:
- runs at startup when its `Enabled` is true, in any environment;
- creates the account once, with `EmailConfirmed = true`, through `UserManager.CreateAsync(user, password)`, so the password policy applies;
- never changes an existing account's password or email;
- refuses (throws) if the email already belongs to an account without the seed's role;
- is coordinated across replicas with the existing transaction-scoped advisory lock.

No credential is stored in tracked files. Local passwords are generated into the ignored `.env`.

1. Refactor `GrumPlates/Features/Accounts/AdminAccountSeed.cs` into `AccountSeed.cs` with one private method `SeedAsync(AccountSeedOptions seed, string role, ...)`, called for `AdminSeed` with role `Admin` and for `UserSeed` with role `User`. Rename `AdminSeedOptions` to `AccountSeedOptions` and add `public AccountSeedOptions UserSeed { get; init; } = new();` to `AccountOptions`. Keep the existing log messages generic ("Configured {Role} account provisioned"); never log the email or password. Use a different advisory-lock key per role, or one lock for the whole seeding run. One lock is simpler; prefer it.
2. Rename `SeedConfiguredAdminAsync` to `SeedConfiguredAccountsAsync` and update `Program.cs`.
3. `AccountOptionsValidator`: apply the existing AdminSeed rules (email and password supplied together, valid mailbox, password required when enabled, password satisfies the policy) to `UserSeed` too, through one shared method. Error messages name the right section.
4. Tests: extend `GrumPlates.Test/Features/Accounts/AdminAccountSeedTests.cs` (rename to `AccountSeedTests.cs`) with User-seed cases: creates a confirmed User; second start changes nothing; refuses an existing account without the role; a weak password fails validation at startup.
5. Delete `GrumPlates/Features/Accounts/Blazor/Seed/LocalLoginAccountSeedExtensions.cs` and its call (`git grep -n SeedLocalLoginAccountsAsync`). This also removes the published `admin@admin.com` / `user@user.com` lockout code. Delete the empty `Seed/` folder and `LocalLoginAccountSeedTests.cs`.
6. Delete the `LocalAccounts` section from `appsettings.Development.json` and `appsettings.Docker.json`; `LocalAccounts__Enabled` from `docker-compose.yml` and the three deployment compose files; `LocalAccounts:Enabled` from `WebAppFactory.cs` (both blocks and the constant) and `ForwardedHeadersTests.cs`; the hits in `ci-docker-smoke.sh`, `test-ci-docker-smoke.sh` and `Deployment/LocalSingleNode/Scripts/validate-rendered-templates.sh`; and the three audit rules.
7. Local credentials (OD-12):
   - `.env.example` (~55-65): add the User seed lines next to the Admin ones, all empty: `Accounts__UserSeed__Enabled=false`, `Accounts__UserSeed__Email=`, `Accounts__UserSeed__Password=`. Rewrite the comment above them: "Local setup fills these with generated values; seeding creates each account once and never resets it." Delete `Accounts__Enabled=false` here too (P03-D also lists it).
   - `docker/setup-local.ps1` today copies `.env.example` to a new `.env`, or appends keys that are missing from an existing `.env` (lines ~10-38). Keep that. Add one function `Set-LocalSeed($Prefix, $Email)` that runs **after** that block for `Accounts__AdminSeed` with `admin@localhost.test` and for `Accounts__UserSeed` with `user@localhost.test`:
     1. Read `.env` lines. A seed is **unconfigured** when its `__Email` and `__Password` values are both empty or absent.
     2. If unconfigured: replace (or append) its three lines with `<Prefix>__Enabled=true`, `<Prefix>__Email=<Email>` and `<Prefix>__Password=<generated>`. Write the file once, keeping every other line and the line order.
     3. If configured (either value non-empty): change nothing.
     4. Generate the password with `[System.Security.Cryptography.RandomNumberGenerator]::GetInt32(...)`: 24 characters from `A-Z`, `a-z` and `0-9`, with at least one of each class. That is safe for raw `.env` parsing and passes the policy (15-128 characters; not in `GrumPlates/Features/Accounts/Security/Resources/10k-most-common.txt`; does not contain the email or app name).
     5. Print "Local Admin: admin@localhost.test, local User: user@localhost.test; passwords are in .env". Never print a password.
   - Keep `Accounts__RegistrationEnabled=false` locally (no SMTP; OD-6).
   - `docker-compose.yml` passes `.env` to the web container (`env_file` with `format: raw`, ~34-36). Confirm with `docker compose config` that the web service receives the six seed keys. Do not paste that output anywhere: it contains the passwords.
   - For `dotnet run` without Docker, document in `docs/HowToRunLocally.md` that the developer sets the same six keys as environment variables. Do not add user secrets.
8. Docs: `docs/HowToRunLocally.md` (~30): "Local setup seeds a local Admin and a local User; their emails are printed by setup and their passwords are in `.env`." `docs/Accounts.md`: replace "Create a local React Admin" with "Local seeded accounts" for both frontends. Remove the sentence about "legacy Blazor demo credentials". Describe `Accounts:UserSeed` in the configuration table.
9. Do not add `UserSeed` to deployment settings. Deployments provision only the Admin, as today.
10. Full local gate.

#### P03-D — React, scripts and deployment settings

1. Remove `accountsEnabled` from the client-config contract and every consumer, in this one PR (OD-11, `schemaVersion` stays `1`):
   - `GrumPlates.Core/Features/Accounts/Contracts/AccountContracts.cs`: the record becomes `ClientConfiguration(int SchemaVersion, string ApplicationId)`. `GrumPlates/Features/Accounts/ClientConfiguration.cs`: stop returning the field.
   - Regenerate the OpenAPI document and the React schema with the React package's script (read `GrumPlates.React/package.json` `scripts`; use the one whose output `scripts/openapi-contract.mjs` checks). Commit `GrumPlates.React/api/openapi.json` and `app/api/generated/schema.d.ts`.
   - React `app/features/accounts/account-session-controller.ts`: delete the `if (!configuration.accountsEnabled)` branch (~169) and the `typeof configuration.accountsEnabled === "boolean"` check (~345). Keep `schemaVersion === 1`. Update every client-config stub in `app/features/accounts/*.test.ts(x)` to `{ schemaVersion: 1, applicationId: ... }`. Delete tests whose only purpose is the disabled mode, for example the `accountsEnabled: false` case in `account-session-controller.test.ts` (~23).
   - `GrumPlates.Test/Features/Accounts/BlazorBooksTransportTests.cs`: stubs become `{"schemaVersion":1,"applicationId":"..."}`.
2. Search React for routes, links or UI hidden when accounts are disabled (`git grep -n -i 'accountsEnabled\|accounts.*disabled\|capabilit' GrumPlates.React/app`). Make them unconditional.
3. `Scripts/Test-DeployedSite.ps1`: keep the client-config shape check (~182-186) but require only `schemaVersion` (= 1) and a non-empty `applicationId`; drop `accountsEnabled` from `$hasShape`. Delete the `AccountsEnabled` capability object (~194) and the disabled branches (~227, 286, 308, 409-414); always run `Invoke-ReactAccountSessionAcceptance`. Update `Scripts/CI/tests/test_deployed_site_acceptance.py`.
4. `Deployment/LocalCluster/Scripts/ci-docker-smoke.sh`: keep the client-config `200` and `no-store` assertions (~220-228) but stop extracting `accounts_enabled`. Keep only the enabled branch at ~283 (`/api/auth/login` returns 405 with `Allow: POST`; `inactive_paths=(/Account /api/auth/not-a-real-operation /assets/missing.js)`). Read the code after ~336 (`docker rm -f "$web"` and the `web-disabled-removed` rename): if it exists only to replace a disabled container with the enabled fixture, keep the fixture and simplify the naming. Update `test-ci-docker-smoke.sh` and the audit rule at ~2989.
5. Remove `accounts_enabled` from: all inventories and examples, the three `app.env.j2` (`Accounts__Enabled=...` lines), Ansible asserts (the `accounts_enabled` parts; keep the rest of each assert, for example "registration needs email host"), `account_deploy_settings.py`, `ls_settings.py` (default and checks ~112-133), both `validate-rendered-templates.sh` (`"accounts_enabled": True` cases become the default case), audit rules (~1911, 2007), `.env.example` (`Accounts__Enabled=false`), `Deployment/LocalCluster/Settings.md`, `Deployment/Cloud/Setup.md`.
6. Docs: rewrite the capability table in `docs/Accounts.md` for the always-on model (two rows: without email; with email). Update `docs/Requirements.md:38,40` ("when the account feature is enabled" and "disabled by default" go) and `README.md:3,19` (accounts are part of both frontends).
7. Verify: `git grep -n -i -E 'accounts_enabled|accountsEnabled|Accounts(:|__)Enabled|LocalAccounts'` returns nothing outside `Plans/`. Full local gate, including React lint/test/build and the OpenAPI contract check.

#### P03-E — Blazor E2E uses the seeded User

Goal (OD-6): Blazor E2E signs in with the seeded User from P03-C instead of registering. Local self-registration stays off (no SMTP locally). There is no local mail server.

1. `GrumPlates.Test/E2E/Support/BlazorE2ETestBase.cs`: replace `RegisterUniqueUserAsync(string emailPrefix)` with `SignInAsSeededUserAsync()`. It opens `/Account/Login`, fills `#Input\.Email` and `#Input\.Password`, submits, and waits for the account link with the user's email (the same wait the old method used).
2. Credentials: delete `GrumPlates.Test/E2E/Support/E2ETestCredentials.cs` (it only holds the old shared password) and fix its callers. Add `E2ESeededUser.cs` in the same folder with a static `(string Email, string Password) Resolve()`:
   1. If the environment variables `E2E_USER_EMAIL` and `E2E_USER_PASSWORD` are both non-empty, use them.
   2. Otherwise find the repository root by walking up from `AppContext.BaseDirectory` to the first folder that contains `GrumPlates.sln`, and read `.env` there. Parse raw `KEY=value` lines: skip blank lines and lines starting with `#`, split at the first `=`, do not trim or unquote the value. Use `Accounts__UserSeed__Email` and `Accounts__UserSeed__Password`.
   3. If neither source gives both values, throw with: "Blazor E2E needs the seeded User. Run docker/setup-local.ps1, or set E2E_USER_EMAIL and E2E_USER_PASSWORD." Never include the password in any message, log, trace or screenshot. Do not type it with a Playwright call that logs its arguments; use `FillAsync` on the password field, which Playwright does not echo.
3. Never track the seeded user for deletion: remove the `TrackCreatedUser(email)` call that `RegisterUniqueUserAsync` made. Keep book tracking and cleanup. Tests must not change the seeded user's password, email, 2FA, passkeys or personal data. If a test needs to do that, it must create its own user in the database through the existing cleanup connection, or the test must go.
4. `BooksE2ETests`: replace `RegisterUniqueUserAsync("e2e-books")` with `SignInAsSeededUserAsync()`. Book titles stay unique per run so parallel or repeated runs do not collide.
5. `IdentityE2ETests.Identity_CanRegisterLogoutLoginAndOpenProfile` becomes `Identity_CanLoginLogoutAndOpenProfile` with the seeded User. Registration is no longer covered by browser E2E; confirm it is covered by integration tests (`git grep -n -l -i register GrumPlates.Test/Features/Accounts`). If it is not, add an integration test that registers through the API with the existing `TlsSmtpFixture`.
6. Remove anything that only served E2E registration (for example a shared `E2EPassword` constant) if it has no other caller.
7. Update `docs/Testing/Browser.md`: E2E needs the local stack started by `RunLocal.ps1` with the seeded User, and accepts `E2E_USER_EMAIL` / `E2E_USER_PASSWORD` overrides.
8. Run the Blazor E2E suite locally against the Docker stack as `docs/Testing/Browser.md` describes (`RUN_E2E=1`): `BooksE2ETests` and `IdentityE2ETests` must pass for the BlazorAuto profile. Run the React public and packaged suites too (`Scripts/Testing/RunReactPublicE2E.ps1`); they do not use the seed and must stay green.

### P04 — Remove the legacy release path

Behaviour after: releases come only from GHCR receipts (`ghcr-container`) and manifest schema v2. There is no `actions-artifact` transport, no `legacy-` pin key, no schema-v1 manifest, no "legacy diagnostic archive" maintenance input, and no `--legacy-blazor-default`.

1. Re-verify section 4.2 with read-only commands:
   - `gh run list -R Grumlebob/GrumPlates --workflow cd-localsinglenode.yml -L 5 --json databaseId,conclusion,createdAt` and the same for `cd-localcluster.yml`. For the newest `success` of each: `gh run view <id> -R Grumlebob/GrumPlates --log | grep -m3 'RELEASE_PAYLOAD_TRANSPORT:'` shows `ghcr-container`. P01-B's run counts.
   - `ssh node-demo 'sudo -n ls /opt/books/release-payloads/Grumlebob-GrumPlates /opt/books-react/release-payloads/Grumlebob-GrumPlates'` and `ssh node-main 'sudo -n ls /opt/books/release-payloads'` list only `ghcr-`… entries (or the repository folder that contains them).
   - If anything shows `actions-artifact` or a `legacy-` key, stop P04 and report. Otherwise continue.
2. Delete files: `.github/workflows/pin-localsinglenode-legacy-release.yml`, `Deployment/LocalSingleNode/Scripts/pin-legacy-release.py`, `Deployment/LocalSingleNode/Scripts/Tests/test_pin_legacy_release.py`, `Deployment/Common/Scripts/Component/lib/import_legacy_release_payload.py`, `Deployment/Common/Scripts/Tests/test_import_legacy_release_payload.py`.
3. `pin_release_payload.py`: delete `LEGACY_PIN_KEY`, the `legacy-...` alternative in `ANY_PIN_KEY` (then `ANY_PIN_KEY` equals the GHCR pattern; keep one constant), `_legacy_identity`, and every branch for `transport == "actions-artifact"` and schema-v1 pins. Update `test_pin_release_payload.py`.
4. `release_manifest.py`: `allowed_schema_versions` default `(2,)` (or remove the parameter if every caller passes the default); delete the `schema_version == 1` branch. Update callers and tests.
5. `release_status.py`: delete `LEGACY_PIN_KEY`, the `legacy-` alternative in `PIN_KEY`, the "legacy release marker" checks and the `legacy-artifact-only` status. Update both `test_release_status.py` files and `Deployment/LocalCluster/Scripts/release-status.py` if it maps that status.
6. `find-successful-ci-run.py`: delete the `legacy_artifact` outcome and the code that produces it (~209-219, ~319). Update `test_ci_provenance.py` and any test using that outcome.
7. `prune-actions-artifacts.py`: read `legacy_dispatch_head_sha` and its caller. If it only serves runs from before receipts existed, delete it and its tests; otherwise keep it and record why in the ledger.
8. Workflows:
   - `cd-localsinglenode.yml`, `cd-localcluster.yml`, `cd-cloud.yml`: delete the `legacy_artifact)` case branches and their summary lines, the `legacy-` alternative in the pin-key regexes, and the "Legacy release manifest v1" summary lines.
   - Delete `--legacy-blazor-default` from both CD workflows' `resolve_frontend_profile.py` calls. Then delete the flag from `Scripts/Frontend/resolve_frontend_profile.py` and its tests in `Scripts/CI/tests/test_frontend_profile.py`. Every release since the frontend-profile file was added contains `frontend-profile.txt`. If a CD run cannot find it, failing is correct.
   - `ci.yml`: remove `test_import_legacy_release_payload.py` (~289) and `test_pin_legacy_release.py` (~299) from the unittest steps.
   - `localsinglenode-maintenance.yml`: delete the off-by-default "legacy diagnostic archive" input and its steps (read `Deployment/Common/ReleaseContract.md:19` to find them).
9. Ansible `Deployment/LocalSingleNode/ansible/roles/single_node_stack/tasks/main.yml`: `release_manifest_schema_version` must be `'2'`; delete the v1 conditions (~21, 24, 197) and the `legacy-` pin pattern (~349-352). Do the same in other roles if `git grep -n "schema_version | string" Deployment` finds them.
10. Remove the `legacy` hits in `test_registry_payload.py`, `test_prune_actions_artifacts.py`, `test-verify-release-identity.sh` (both) and `validate-rendered-templates.sh` that belong to this path.
11. Audit: delete the rules that check the removed workflow, scripts and branches. If the audit counts workflows or has an allow-list of workflow files, update it.
12. Docs: update `Deployment/Common/ReleaseContract.md`, `Deployment/LocalSingleNode/Operations.md` and `Deployment/LocalCluster/Operations.md`. Delete the legacy pin/import sections.
13. Verify: `git grep -n -i -E 'legacy[-_ ]?(artifact|pin|release|manifest)|actions-artifact|schema v1|schema-v1|legacy-blazor-default'` outside `Plans/` returns nothing. Full local gate.

### P05 — CI runner: keep the fixed path, remove the unused routing

Decided by OD-8: remove the routing.

Target shape (OD-15) of `.github/workflows/ci.yml`: the jobs are `validate`, `publish-main`, `notify-dependabot-automerge` and `ci-result-gate`. All four use `runs-on: [self-hosted, linux, x64, "${{ vars.CI_RUNNER_LABEL }}"]`. There is no selector job.

1. `validate` (~56): delete `needs: select-validation`. Move the selector's `if:` (`github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository`, ~35) onto `validate`, merged with any `if:` it already has. Set `runs-on` as above.
2. `publish-main` (~597): `needs: [validate]`. Keep its existing `if:` conditions, minus any reference to `select-publication`. Set `runs-on` as above.
3. Delete the jobs `select-validation` (~33-54) and `select-publication` (~575-595), the comment above `validate` about the desktop route, the `pool` `workflow_dispatch` input (keep the bare `workflow_dispatch:` trigger), and every expression that reads `needs.select-*` or `routing_enabled`. Keep the `ci-result-gate` job `name:` expression that produces `build-test-push` (~1005), and change its fallback `'CI routing result'` to `'CI result'`.
4. Rename the steps "Verify legacy CI runner" (~84 and ~650) to "Verify CI runner" and delete their `if:` lines. At the top of their `run:`, add the label check that the selector used to do (`select-ci-runner.yml` ~154-160): read `CI_RUNNER_LABEL: ${{ vars.CI_RUNNER_LABEL }}` from `env:`, fail with "Set the CI_RUNNER_LABEL repository variable" when it is empty, and fail when it does not match `^[A-Za-z0-9][A-Za-z0-9_.-]*$`. Keep the existing host and workspace checks unchanged.
5. Delete the steps "Verify selected CI runner profile" (`git grep -n 'selected CI runner profile' .github`).
6. Replace every `vars.CI_RUNNER_LABEL || vars.LOCALCLUSTER_RUNNER_LABEL || 'localcluster-books'` in `ci.yml` (~966, ~1008) with `vars.CI_RUNNER_LABEL`. Leave the CD workflows' own `LOCALSINGLENODE_RUNNER_LABEL` / `LOCALCLUSTER_RUNNER_LABEL` expressions alone; they are a different variable.
7. Result gate:
   - `ci-result-gate`: `needs: [validate, publish-main]`; delete `CI_VALIDATION_SELECTION_RESULT` and `CI_PUBLICATION_SELECTION_RESULT` from its `env:`.
   - `Scripts/CI/require_ci_results.py`: delete the `validation_selection` and `publication_selection` parameters, their checks (~32-41) and their `os.environ` reads (~50-52). Keep the rules for `validation` and `publication`: `main` push needs both `success`; other events need validation `success` and publication `skipped`.
   - Update `Scripts/CI/tests/test_require_ci_results.py` the same way.
8. Delete files: `.github/workflows/select-ci-runner.yml`, `Scripts/CI/runner_profiles.py`, `select_ci_runner.py`, `verify_ci_runner.py`, `Scripts/CI/tests/test_runner_profiles.py`, `test_select_ci_runner.py`, `test_verify_ci_runner.py`, `test_ci_runner_routing_workflow.py`, `docs/CI/RunnerProfiles.md`. Remove them from the unittest step at `ci.yml` ~202 and rename that step "Test frontend profile and CI result contracts".
9. `git grep -n -E 'CI_RUNNER_PROFILES_JSON|CI_RUNNER_INVENTORY_APP|CI_RUNNER_ROUTING_ENABLED|routing_enabled|select-ci-runner|runner_profiles|RunnerProfiles|template-desktop|native-legacy' -- ':!Plans'` must return nothing. Fix `Scripts/CI/workflow_policy.py`, its tests, the audit (delete rules from PR #376 that check removed parts), `docs/HowToForkThisRepo.md`, `docs/Test.md` (line 23 and gate line 35) and `AGENTS.md`.
10. Full local gate. On the PR, confirm `validate` and `build-test-push` ran on runner label `localsinglenode-books` (the job log's "Runner name" line) and passed.
11. After merge and green `main` CI: `gh variable delete CI_RUNNER_ROUTING_ENABLED -R Grumlebob/GrumPlates`. Record it as evidence.

### P06 — Remove other one-time migration code

1. Delete `Deployment/LocalCluster/Scripts/prepare-renamed-localcluster-app.sh` and `cleanup-renamed-localcluster-app.sh`, their section in `Deployment/LocalCluster/Operations.md`, and any test, audit rule, `ci.yml` shellcheck list entry or doc link to them (`git grep -n renamed-localcluster-app`).
2. Caddy: read `Deployment/Common/Scripts/Tests/test_caddy_install.py` and the code it tests (`git grep -n -l caddy Deployment/Common`). Delete only code that migrates from an old Caddy apt repository or keyring, and its tests. Keep the install, the checksum policy and Q-002's current MD5 check (separate backlog item). Rename the `ci.yml` step to "Test Caddy install safeguards", or drop the step if the test file is gone.
3. Delete the "Remove legacy Docker apt repository list" task in `Deployment/Cloud/ansible/roles/docker/tasks/main.yml` (~30), and the same in other roles if found.
4. `Deployment/Cloud/Scripts/set-temporary-ssh-firewall.sh`: delete `legacy_role_match`, `legacy_name_match` and the "Expected one legacy temporary SSH firewall" path. Keep matching for the current label/name scheme.
5. Delete `ServerProject_HasNo_Legacy_Root_Infrastructure_Folders` from `GrumPlates.Test/Architecture/Boundaries/NoInfrastructureNamespaceTests.cs` (an absence test).
5a. `cd-localcluster.yml`: delete the `refresh_rating_ranks` input (~30), its `INPUT_REFRESH_RATING_RANKS` env line (~217) and both rejection checks (~238, ~251). Remove `"refresh_rating_ranks"` from the expected input list in `Deployment/Common/Scripts/Tests/test_release_status.py:34` and from any audit rule (`git grep -n refresh_rating_ranks`).
5b. `audit_deployment.py` (~1682): delete the rule that checks for the old workflow name "Deploy Ship To LAN". Search for other old-name absence rules with `git grep -n -i -E 'ship to lan|BlazorAutoApp|improveddb' Deployment/LocalCluster/Scripts/Component/lib/audit_deployment.py` and delete the ones that only assert an old name is absent.
6. Search for remaining one-time code with: `git grep -n -i -E 'legacy|earlier versions|previous versions|pre-receipt|old app name|renamed app|one-time|migrate from|backward.?compat' -- ':!Plans' ':!*Migrations*' ':!*package-lock.json'`. For each hit decide: remove (old-state handling), keep (the word is used for something current, such as the upstream patch in `patch-redocly-minimatch.mjs` or Sonar's "legacy issue" type), or move to another packet. Record each decision in the PR description.
7. Full local gate.

### P07 — Rename the `ApplicationUser` namespace and squash the migrations (one PR, OD-7, OD-14)

Prerequisite: P01-B is complete (LocalCluster has all four old migrations). Goal: one migration, `InitialSchema`, produces exactly the schema the four old migrations produce, so live databases keep their data (P14 step 3).

Facts: the old migrations contain raw `Sql(...)` only for data conversion and preflight guards (`UserOwnedBooks` deletes old books; `NormalizeBooksForAuthorBooks` copies owners into `UserBooks`; `AccountOutboxAndDataProtectionKeys` checks for duplicate emails). None of it creates schema objects, so `InitialSchema` ports none of it. No migration seeds data; author books are seeded at startup (`AuthorBooks:SeedAtStartup`).

Tools: `dotnet tool restore` installs `dotnet-ef` 10.0.14 from `.config/dotnet-tools.json`. `GrumPlates/Infrastructure/Persistence/DesignTimeDbContextFactory.cs` reads the connection string from the environment variable `ConnectionStrings__DefaultConnection`. Run Docker commands where Docker works (Windows Docker Desktop or the WSL gate environment). Keep schema dumps under the ignored `Plans.local/squash/` folder.

1. **Capture the old schema, on unchanged `main`, before editing anything.**
   1. `docker run --rm -d --name squash-old -e POSTGRES_PASSWORD=squash -e POSTGRES_DB=app -p 127.0.0.1:55432:5432 postgres:18.4-alpine3.23` (the image from `docker-compose.yml`). Wait until `docker exec squash-old pg_isready -U postgres -d app` succeeds.
   2. `ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=55432;Database=app;Username=postgres;Password=squash" dotnet ef database update --project GrumPlates --startup-project GrumPlates`.
   3. `mkdir -p Plans.local/squash && docker exec squash-old pg_dump -U postgres -d app --schema-only --no-owner --no-privileges > Plans.local/squash/old-schema.sql`.
   4. `docker rm -f squash-old`.
2. **Namespace.** Change the namespace in `GrumPlates/Features/Accounts/Identity/ApplicationUser.cs` from `GrumPlates.Features.Login.Account` to `GrumPlates.Features.Accounts.Identity`. Fix every `using ApplicationUser = GrumPlates.Features.Login.Account.ApplicationUser;` alias and `@using GrumPlates.Features.Login.Account` line (`git grep -l 'GrumPlates.Features.Login.Account' -- ':!Plans' ':!*Migrations*'`, about 40 files, including `GrumPlates/Components/_Imports.razor` and `Features/Accounts/Blazor/_Imports.razor`). Do not edit the old migration files; step 3 deletes them.
3. **Squash.** Delete every file in `GrumPlates/Infrastructure/Persistence/Migrations/` (four migrations, their `.Designer.cs` files and `AppDbContextModelSnapshot.cs`). Run `dotnet ef migrations add InitialSchema --project GrumPlates --startup-project GrumPlates --output-dir Infrastructure/Persistence/Migrations`. `dotnet build GrumPlates.sln -c Release` must pass.
4. **Prove equivalence.** Repeat step 1 with container name `squash-new` on the new code, writing `Plans.local/squash/new-schema.sql`. Normalise both dumps with `grep -v -E '^\\(un)?restrict |^-- Dumped (from|by)'` (pg_dump 18 writes random `
estrict` tokens and version headers), then `diff -u` the normalised files. It must print nothing. If it differs, fix the EF model configuration (not raw SQL) until it matches, regenerate `InitialSchema` (delete and re-add), and diff again. If a difference cannot be fixed in the model, stop and report it. Put "schema diff: identical" in the PR description.
5. `dotnet ef migrations has-pending-model-changes --project GrumPlates --startup-project GrumPlates` reports no changes.
6. Architecture test: rewrite `AccountsBoundaryArchitectureTests.ProductionAssemblies_RetainOnlyTheMappedUserInLegacyLoginNamespaces` (`GrumPlates.Test/Architecture/Slices/AccountsBoundaryArchitectureTests.cs:8-28`) as "no production type lives under `GrumPlates.Features.Login`". If `FeatureSlicesArchitectureTests` already enforces namespace-per-folder, delete it instead.
7. Migration provenance: update `Scripts/CI/migration_staging_artifact.py`, its tests, `Scripts/CI/tests/fixtures/dotnet-ef-migration-list.json`, release-manifest migration-ID checks (`git grep -n -i 'migration.*id' Deployment/Common/Scripts Scripts/CI`), and every test that names an old migration ID. `git grep -n -E '2026052[0-9]{6}_|20261015183858' -- ':!Plans'` must return nothing.
8. Record in the PR description and the ledger, for P14: the new migration ID (`<timestamp>_InitialSchema`, from the file name) and the `ProductVersion` from `InitialSchema.Designer.cs` (`.HasAnnotation("ProductVersion", "...")`).
9. Docs: `docs/Accounts.md` (delete the sentence saying `ApplicationUser` keeps the old namespace). `docs/HowToRunLocally.md`: "After pulling the migration squash, reset your local database once: `docker compose down` then `docker volume rm <project>_pgdata`, then start again." Find the real volume name with `docker volume ls`. This applies to local Docker only, never to nodes.
10. Full local gate. CI's Testcontainers databases are created fresh, so they exercise `InitialSchema`.
11. Merging is safe because CD runs only by manual dispatch. From this merge until P14 step 3, **never** dispatch CD with `run_migrations=true` (section 5).

### P08 — Single source for the Node version; then fix #406

Behaviour after: Dependabot's Docker bump of `node:X-bookworm-slim` in `GrumPlates/Dockerfile` is the only edit needed to move Node within major version 26.

1. Source of truth: the first `FROM node:<version>-bookworm-slim` line in `GrumPlates/Dockerfile`.
2. `GrumPlates.React/package.json` `engines.node`: change `"26.12.1"` to `">=26.12.1 <27"`. Keep `engines.npm` exact (`12.2.0`) because the Dockerfile installs exactly that. Run `npm install --package-lock-only` from `GrumPlates.React` and commit the lockfile if it changes. Check that `fnm` on Windows still selects 26.x in that folder.
3. `ci.yml`: add a step before "Setup Node" that reads the version: `node_version="$(sed -n -E 's/^FROM node:([0-9]+\.[0-9]+\.[0-9]+)-bookworm-slim.*/\1/p' GrumPlates/Dockerfile | head -n1)"`, fails if empty, and writes `node_version=$node_version` to `$GITHUB_OUTPUT`. Use `${{ steps.<id>.outputs.node_version }}` in `actions/setup-node` (~379) and in the version check (~434).
4. `Deployment/LocalCluster/Scripts/ci-docker-smoke.sh:44`: derive `SMTP_FIXTURE_IMAGE` from the same Dockerfile line, with the same `sed`. Update `test-ci-docker-smoke.sh:272` to assert the derived value, not a literal.
5. Both `validate-rendered-templates.sh` (`'image': 'node:26.12.1-bookworm-slim'`): derive the tag the same way (Python: read the Dockerfile and match the regex).
6. `Scripts/Testing/RunReactPublicE2E.ps1:135-151`: read the version from the Dockerfile instead of the literal.
7. `audit_deployment.py:985`: replace the literal rule with a rule that `ci.yml` derives the Node version from `GrumPlates/Dockerfile`.
8. Verify: `git grep -n '26\.12\.1' -- ':!Plans' ':!*package-lock.json'` returns only `GrumPlates/Dockerfile` and the `>=26.12.1` engine range. Full local gate.
9. After merge, comment `@dependabot rebase` on #406. It should pass now. Merge it when green on the exact head. If Dependabot closed or superseded it, record the replacement PR number instead.

### P09 — Configuration and code tidy

1. **Settings files.** Move the `Database:*` placeholders from `settings.defaults.json` into `appsettings.json`. Keep `Redis:Configuration` as it is in `appsettings.json` (`localhost:6379`), but first check `AppOptionsExtensions.IsPlaceholder` and every test that expects the `INJECT_THIS_IN_ORDER_TO_RUN` placeholder for Redis (`git grep -n INJECT_THIS`). If behaviour depends on Redis being a placeholder by default outside Development, keep the placeholder in `appsettings.json` and set `localhost:6379` in `appsettings.Development.json`. Then delete `settings.defaults.json` and its `GrumPlates.csproj` entry.
2. **Configuration sources.** Delete `AppConfigurationExtensions.cs` and its call in `Program.cs`, because `WebApplication.CreateBuilder` already loads `appsettings.json`, `appsettings.{Environment}.json` (which covers `Docker`) and environment variables. Check `OpenApiMetadataHosting.CreateBuilder` and `WebAppFactory` do not rely on it. Add one test that the `Docker` environment reads a value from `appsettings.Docker.json`, if no existing test proves it.
3. **`.env.example`.** Decide one source for each OpenTelemetry value. Read how `docker-compose.yml` passes `OBSERVABILITY_*` and `Observability__OpenTelemetry__*` to the web container and Alloy, and how `Scripts/RunLocal.ps1` sets them (lines ~20-24, 153-154, 287). Keep the `OBSERVABILITY_*` interpolation variables and map them to `Observability__OpenTelemetry__*` inside `docker-compose.yml`'s web `environment:`. Delete the duplicated `Observability__OpenTelemetry__*` lines from `.env.example`. Delete the stale "Later observability phases..." comment. Verify: before editing, save `docker compose config --format json` with a test `.env` (copy `.env.example` to a scratch folder; never use the developer's real `.env`) to `Plans.local/compose-before.json`; after editing, save `compose-after.json` the same way; compare the web service's `environment` with `python3 -c` or `jq`. Only the removed duplicates may differ, and the values the app reads must stay the same.
4. **Program.cs ProblemDetails.** Move the `/api/auth` code mapping (`Program.cs:26-47`) into the Accounts feature, for example `Features/Accounts/AccountProblemDetails.cs` with `services.AddAccountProblemDetails()`, called from `AddAccountFeature` or from `Program.cs` in its place. Keep behaviour identical; existing account tests must pass unchanged.
5. **`DataProtectionKeyStorageOptions`** in its own file (if P03-B did not already).
6. **`Plans/README.md`:** delete the line "Older retained plans, if any, live in `docs/plans/`." (Or do it in P12.)
7. Full local gate.

### P10 — Consolidate CI unittest steps

1. In `ci.yml` `validate`, list every step whose `run:` is only `python3 -m unittest <file(s)>` or `python3 -m unittest discover ...`.
2. Replace them with one step per test directory, in the original order of first appearance. The test folders have no `__init__.py`, so do **not** pass `-t .`. Run from the repository root; `python3 -m` puts the current directory on `sys.path`, so imports such as `Scripts.Quality...` keep working.
   - `python3 -m unittest discover -s Scripts/CI/tests -v`
   - `python3 -m unittest discover -s Scripts/Quality/tests -v`. This replaces the separate "Validate quality audit runner" step (`ci.yml` ~75, `python3 -m unittest Scripts.Quality.tests.test_run_quality_audit`). Keep this step at that early position.
   - `python3 -m unittest discover -s Deployment/Common/Scripts/Tests -v`
   - `python3 -m unittest discover -s Deployment/LocalCluster/Scripts/Tests -p 'test_*.py' -v`
   - `python3 -m unittest discover -s Deployment/LocalSingleNode/Scripts/Tests -v` (already a discover step)
   - Cloud has only shell tests (`Deployment/Cloud/Scripts/Tests/*.sh`); leave them as they are.
   First run each discover command locally and compare its test list with the union of the old steps (`-v` prints names). A test that only passes in its own process, or needs a step-specific `env:` or tool (for example `command -v pwsh` before `test_deployed_site_acceptance.py`, ~204-207), keeps its own step and is excluded from discovery with a narrower `-p`, or the discover step gets that `env:`/check. Note the reason in a YAML comment. Every test file must run exactly once.
3. Keep the non-unittest steps (lint, audits, shell tests, builds, smoke) as they are.
4. Update `docs/Test.md` local-gate lines 34-36 and 44 to the same discover commands.
5. Update `Scripts/CI/workflow_policy.py` and the audit if they reference removed step names.
6. Full local gate; then confirm on the PR that `validate` runs and the total number of executed Python tests is not lower than before. Compare the two run logs' `Ran N tests` lines; record both totals in the PR description, not in any file.

### P11 — Stop the deployment audit from reading Markdown

Decided by OD-9.

1. List every rule in `audit_deployment.py` that reads a `.md` file directly or through `doc_slice_loader.py` (`git grep -n -E 'doc_slice|\.md' Deployment/LocalCluster/Scripts/Component/lib/audit_deployment.py`).
2. Delete those rules. Rules that check code, workflows, templates, compose files and settings stay.
3. If `doc_slice_loader.py` then has no callers, delete it and `Deployment/LocalCluster/Scripts/Tests/test_doc_slice_loader.py`, and remove the gate line `python3 -m unittest Deployment/LocalCluster/Scripts/Tests/test_doc_slice_loader.py -v` from `docs/Test.md`.
4. `AGENTS.md`: delete the sentence "The deployment audit reads Markdown, so a docs-only change under `Deployment/` or `docs/` still runs `bash Deployment/LocalCluster/Scripts/audit-deployment.sh` before merge."
5. Full local gate. Prove the change: on a scratch branch, edit a sentence in `Deployment/README.md` and confirm `bash Deployment/LocalCluster/Scripts/audit-deployment.sh` still passes. Discard the scratch branch.
6. From this merge on, the docs-only rule in section 3 no longer needs the audit run. P15 updates the agent memory that says otherwise.

### P12 — Plans and docs cleanup

1. Delete every file under `Plans/` except `README.md`, `LeanTemplateCleanup.md`, `LeanTemplateCleanupGoal.md`, `CodeQualityImprovements.md` and `CloudflaredConnectorFollowUps.md`. Delete the whole `Plans/Support/` tree. `Plans/Support/PortfolioStudio/JobHuntWorkflow.html` is HTML, so this one PR is **not** docs-only: run the full local gate and let CI run (no `[skip ci]`).
2. Before deleting, run `git grep -n 'Plans/' -- ':!Plans'` and fix every link to a deleted plan in `README.md`, `AGENTS.md`, `docs/`, `Deployment/` and code comments (for example `Scripts/Quality/sonar_report.py:53` points to `Plans/CodeQualityImprovements.md`, which stays). Check `Deployment/Common/Scripts/Tests/test_operation_source.py:67`, which lists `Plans/Delivery.md` as a path fixture; leave it if it is only test data.
3. `Plans/CodeQualityImprovements.md`: delete the completed and superseded entries (Q-001, Q-005, Q-006, Q-007, Q-008). Keep Q-002, Q-003, Q-004 unchanged.
4. `Plans/README.md`: delete the `docs/plans/` sentence if P09 did not.
5. Observability docs: read `docs/ObservabilityGuide.md`, `docs/Observability/Architecture.md` and `docs/Observability/Operations.md`. Merge the guide into `docs/Observability/Architecture.md` (or the reverse) so each fact appears once, and fix links (`git grep -n ObservabilityGuide`).
6. Re-read `docs/Accounts.md`, `docs/HowToRunLocally.md`, `docs/HowToForkThisRepo.md`, `docs/Requirements.md`, `README.md` and `Deployment/README.md` once more for mentions of removed features (legacy import, disabled accounts, `LocalAccounts`, runner profiles, legacy pins, rename scripts). Fix any you find.
7. Run `git diff --check` and the full local gate.

### P13 — Local junk cleanup (owner's PC; nothing is committed)

All of these are ignored or untracked files in the primary checkout `C:\Users\jgrum\Documents\Programming\Csharp\GrumPlates`. Check `git status --ignored --short` first, and never delete a tracked file.

1. Delete build output: `GrumPlates/bin`, `GrumPlates/obj`, `GrumPlates.Client/bin`, `GrumPlates.Client/obj`, `GrumPlates.Core/bin`, `GrumPlates.Core/obj`, `GrumPlates.Test/bin`, `GrumPlates.Test/obj`, `GrumPlates.React/build`. They still contain `BlazorAutoApp*` names, including `BlazorAutoApp.Simulation`. Only do this when no build, test or IDE is running in that checkout.
2. Delete `GrumPlates/BlazorAutoApp.csproj.user` (stale IDE file for the old project name).
3. Delete every `__pycache__` folder under `Deployment/` and `Scripts/`.
4. Do **not** delete: `.env`, `.env.cloud`, `docker/https/aspnetapp.pfx`, `Plans.local/`, `GrumPlates.sln.DotSettings.user`, or anything under `Deployment/Cloud/infra/opentofu/` (real Cloud state and plans) or `Deployment/Cloud/inventory/prod/hosts.yml`.
5. Leave `agent.md` alone. It is the owner's private, ignored notes file (see section 10).

### P14 — Rewrite migration history, deploy and verify the live targets

Authorised by the goal file: backups, the history rewrite on the three `books` databases in section 4.2, the five dispatches below, and release-status runs. Nothing else on the hosts. Never dispatch Cloud. Never drop a database, delete a volume, prune, or release a lock you did not take.

Expected effects, which are not failures: accounts are now on everywhere. Data Protection keys move from Redis to PostgreSQL, so existing sign-in cookies and antiforgery tokens stop working once and users sign in again. Registration stays off where no SMTP is configured.

1. **Preconditions.**
   - P02 to P12 are merged. `target_sha` = the newest `main` commit with a `success` CI push run (`gh run list -R Grumlebob/GrumPlates --workflow ci.yml --branch main --event push -L 5 --json headSha,conclusion`). Use it for every dispatch in this packet.
   - `gh run list -R Grumlebob/GrumPlates --status in_progress` and `--status queued` show no CD, maintenance or retention run. Work outside 03:00-05:00 UTC.
   - You have `<NEW_ID>` and `<PRODUCT_VERSION>` from P07 step 8, and `Plans.local/squash/new-schema.sql` from P07 step 4. If they are missing, regenerate them: check out `target_sha` and repeat P07 step 4 for the new schema only.
2. **Backups.** Wait for each to finish green and record the run ID or backup path.
   - LocalSingleNode primary: `gh workflow run localsinglenode-maintenance.yml -R Grumlebob/GrumPlates --ref main -f app_instance=primary -f backup_now=true`.
   - LocalSingleNode additional: the same with `-f app_instance=additional`.
   - LocalCluster: `ssh node-db 'cd /opt/books && sudo -n -u deploy ./backup-db.sh'`. It must print `backup verification ok` and a `backup path:`.
   - If any backup fails, stop P14 and report.
3. **History rewrite**, once per database. `<host> <container> <db/user>` are `node-demo books-postgres-1 books`, `node-demo books-react-postgres-1 books_react`, `node-db books-postgres-1 ship`.
   1. **Live schema check.** `ssh <host> "sudo -n docker exec <container> pg_dump -U <user> -d <db> --schema-only --no-owner --no-privileges" > Plans.local/squash/live-<container>-<host>.sql`. Normalise both files with `grep -v -E '^\\(un)?restrict |^-- Dumped (from|by)'` and `diff` them against `new-schema.sql`. They must be identical. If they differ, stop and report the diff; do not rewrite that database.
   2. Write `Plans.local/squash/rewrite.sql` once, with the values from P07 step 8:
      ```sql
      BEGIN;
      DO $$
      BEGIN
        IF (SELECT count(*) FROM "__EFMigrationsHistory") <> 4
           OR (SELECT count(*) FROM "__EFMigrationsHistory" WHERE "MigrationId" IN (
                 '20260525172002_InitialTemplateSchema',
                 '20260526083400_UserOwnedBooks',
                 '20260528104203_NormalizeBooksForAuthorBooks',
                 '20261015183858_AccountOutboxAndDataProtectionKeys')) <> 4
        THEN
          RAISE EXCEPTION 'unexpected migration history; nothing changed';
        END IF;
      END $$;
      DELETE FROM "__EFMigrationsHistory";
      INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion") VALUES ('<NEW_ID>', '<PRODUCT_VERSION>');
      COMMIT;
      ```
   3. Run it: `ssh <host> "sudo -n docker exec -i <container> psql -v ON_ERROR_STOP=1 -U <user> -d <db>" < Plans.local/squash/rewrite.sql`. If it raises the exception, nothing changed: stop and report.
   4. Confirm: `ssh <host> "sudo -n docker exec <container> psql -U <user> -d <db> -At -c 'SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\"'"` prints only `<NEW_ID>`.
   The running apps do not read the history (`RunMigrationsAtStartup` is false), so they keep working during this step.
4. **Deploy**, one at a time. Wait with `gh run watch <id> -R Grumlebob/GrumPlates --exit-status`:
   1. `gh workflow run cd-localsinglenode.yml -R Grumlebob/GrumPlates --ref main -f app_instance=primary -f run_migrations=true -f target_sha=<target_sha>`
   2. the same with `-f app_instance=additional`
   3. `gh workflow run cd-localcluster.yml -R Grumlebob/GrumPlates --ref main -f run_migrations=true -f target_sha=<target_sha>`
   In each log, the migration bundle must apply nothing (EF prints "No migrations were applied"). If it tries to apply `InitialSchema`, the rewrite did not happen on that database. The run then fails because the tables exist. Stop and report; do not restore automatically. The step 2 backup is the recovery point.
   Each CD run's own acceptance step (`Scripts/Test-DeployedSite.ps1`) must be green.
5. **Release status:** `gh workflow run release-status.yml -R Grumlebob/GrumPlates --ref main -f target=localsinglenode -f app_instance=primary`, then `-f app_instance=additional`, then `-f target=localcluster -f app_instance=primary`. Each must be green and report the deployed `target_sha` as a GHCR receipt release.
6. Delete `Plans.local/squash/live-*.sql` (they contain only schema, but keep the folder tidy). Record all run IDs, backup paths, SHAs and the history-check output as evidence.

### P15 — Closeout

1. `git fetch --all --prune`. Confirm `origin/main` contains every packet's merge and that its CI is green.
2. Confirm `gh pr list -R Grumlebob/GrumPlates --state open` is empty (or only new Dependabot PRs created after this plan, which get the normal treatment).
3. Confirm `git branch -a` shows only `main`, `origin/main`, `origin/HEAD` and the `improveddb/*` remote branches. Delete every finished `cleanup/lean-*` branch locally and on GitHub, and record each full SHA in the ledger. Remove task worktrees with `git worktree remove` from the primary checkout.
4. Fast-forward the primary checkout's `main` if it is clean and idle.
5. Run the final residue search and paste the result into the ledger:
   ```bash
   git grep -n -i -E 'legacy|accountsEnabled|Accounts(:|__)Enabled|LocalAccounts|data_protection_legacy|renamed-localcluster|26\.12\.1' -- ':!Plans' ':!*package-lock.json' ':!*Migrations*'
   ```
   Every remaining hit must have a recorded "keep" reason from P06 step 6.
6. Mark this plan **complete** in its status line, fill the last ledger rows, and merge that as a docs-only PR (`[skip ci]`, `git diff --check`). Verify the squash commit is on `origin/main`, then delete its branch.
7. Agent memory. If you run as Claude Code with this project's memory folder (`~/.claude/projects/C--Users-jgrum-Documents-Programming-Csharp-GrumPlates/memory/`): delete `docs-skip-ci-can-break-audit.md` (P11 made it obsolete) and its `MEMORY.md` line, and change `grumplates-rename-plan.md` so it no longer points to the deleted `Plans/GrumPlatesRename.md` (the rename is recorded in git history, PR list and `docs/`). Keep `no-exact-test-count-pins.md`. Otherwise, list these changes for the owner.
8. Downstream closeout: `gh pr list -R Grumlebob/<Repo> --state open` for CarbonCalculator, ImprovedDb, Portfolio and JobHunt shows no PR that D01-D05 should have unblocked (only newer Dependabot PRs, handled the normal way). Every D-packet worktree is removed. Report the kept downstream branches from D10 with their one-line summaries.
9. Final report to the owner: what changed in each repository, PR numbers, deploy run IDs, backup paths, kept branches, and that D09's CD changes get their first live use on the owner's next normal deploy of CarbonCalculator and Portfolio.

### Disk packets P16–P20 (added 2026-11-04)

These packets make the template's own retention work, remove the template's rename-era leftovers once, and report the rest to the owner. Facts and causes are in section 4.12. Rules:

- Only books-owned resources are changed: names, labels or image repositories starting with `books`, `private-books` or this repository's CI labels. Never touch CarbonCalculator, ImprovedDb or Portfolio host resources.
- No `docker system|image|builder|container|volume prune` typed by hand, no `--volumes`, no `-v` on `docker rm`, and no `--force` on `docker image rm`. Volumes are never removed. The repository's own scripts may prune build cache as they already do (P19).
- Recheck immediately before each removal that nothing running or stopped uses the image, and that it is not the current or recovery release or a pinned digest.
- Record `df -m /opt` before and after every host step.
- Fixes stay current-state only (no legacy-name code, as in P06). Old-name leftovers go through the one-time P18 instead.

#### P16 — Old app images: count real tags only

1. In `Deployment/LocalSingleNode/Scripts/lib/maintenance.py`, the multiple-tag guard in `candidates()` counts only `RepoTags` entries without `@`. A digest entry names the same image, so removing by ID needs no force. Two real tags still mean report only. Check the other `RepoTags` uses in the file (the current-release lookup) and in `Deployment/LocalCluster/Scripts/prune-docker-residue.sh` and `protected-release-images.sh` for the same assumption.
2. Tests in `Deployment/LocalSingleNode/Scripts/Tests/`, using Docker 29's shape (`RepoTags: [tag, name@sha256:…]`):
   - a candidate whose digest is in `RepoTags` is removed;
   - an image with two real tags stays report-only;
   - a container reference or a protected digest still keeps the image.
3. Prove on node-demo, read-only, that `docker image rm <id>` without force removes an image that has one tag plus its digest. Use a `docker image rm --help` check, plus a throwaway image you tag yourself (`busybox` pulled by tag and digest, then removed). Never test on an app image.
4. Focused tests, full local gate, PR, merge, main CI green.
5. Verify. Dispatch `localsinglenode-maintenance.yml` with `app_instance=primary`, then with `additional`. The logs must show "Removed proven old app image" for books and books-react images older than 48 hours, and no refusals. Record the image counts and the node-demo free space before and after.

#### P17 — LocalCluster scoped cleanup runs daily

1. In `.github/workflows/localcluster-docker-maintenance.yml`, let the `prune` job also run on the existing `17 3 * * *` schedule. Keep its default mode:
   - only resources this repository owns;
   - no `--include-unlabelled-host-residue`;
   - no `--allow-shared-builder-prune`;
   - volumes only reported.

   Keep `artifact_only` for dispatches. Both jobs already share the deployment lock and a concurrency group.
2. Update `Deployment/LocalCluster/Operations.md` ("Host and Docker cleanup are manual") and any workflow-policy test or audit rule that pins the manual-only trigger. Change the rule's expectation; never delete the rule.
3. When `check-node-main-capacity.sh` fails, its message names the maintenance workflow and the Operations section to run first.
4. Focused tests, full local gate, PR, merge, main CI green.
5. Verify by dispatching the workflow once (`artifact_only=false`). The `prune` job must succeed and list only books-owned removals. Then confirm the next scheduled run, record node-main free space, and check that ImprovedDb's resources are unchanged (its image and container counts before and after).

#### P18 — One-time removal of rename-era leftovers (hosts, no code)

Run only while no GrumPlates CD or maintenance run is in progress or queued (`gh run list -R Grumlebob/GrumPlates --status in_progress` and `--status queued`). List the candidates first, then remove them one by one under the rules above.

1. node-main, CI session `38004425567-1`:
   - stop and remove the containers `books-ci-web-38004425567-1`, `books-ci-redis-38004425567-1` and `books-ci-postgres-38004425567-1` (no `-v`);
   - remove their Compose network if nothing else uses it;
   - then remove `ghcr.io/grumlebob/books:a5bd0287e4694ad7f3fe60fdff018ee0648244e7-38004425567-1`;
   - report any volumes left behind; do not delete them.
2. node-main: remove the stopped containers labelled `localcluster.ci.repository=Grumlebob/BlazorAutoAppTemplate` (owner `tests` or `ci-smoke`) without `-v`. Unlabelled stopped containers are not ours; list their count only.
3. node-demo: remove the `ghcr.io/grumlebob/books` and `books-react` images whose `org.opencontainers.image.source` is `https://github.com/Grumlebob/BlazorAutoAppTemplate`. Skip any that are in use, current, recovery or pinned under `/opt/books*/release-payloads`. Also remove the untagged `private-books*-release-payload` images with that source, if any.
4. Record every removed ID, all volumes left, and free space before and after in the ledger.

#### P19 — Keep the BuildKit cache within its budget

1. Read-only first. Compare `docker buildx du --verbose` on node-demo with the `build_cache.py` logs of the last runs. Find why the cache stays near 40 GB against a 20 GB budget. Likely causes:
   - the Docker 29 `--max-used-space` / `--reserved-space` flags trim reclaimable records only;
   - shared or in-use records are not counted;
   - `builder.gc` in `daemon.json` never triggers.

   Write the finding into the PR.
2. Fix `Deployment/LocalSingleNode/Scripts/lib/build_cache.py` (and its tests) so that one run brings the cache to the budget, or explain in Q20 why the budget cannot be met and set a realistic value in `BUILD_CACHE_KEEP_GB`'s documented default. Do not change `daemon.json` (applying it restarts Docker for every app on the node). If a daemon change is the only fix, record it as an owner action.
3. Focused tests, full local gate, PR, merge, main CI green.
4. Verify on the next `localsinglenode-maintenance` run: "after" is at or under the budget. Record before and after.

#### P20 — Disk closeout

1. Repeat the section 4.12 measurement on all six nodes and put a before/after table in the ledger.
2. Confirm that `cd-localcluster.yml`'s capacity check would pass (`df -m /opt` against 20480 MiB; no deploy needed) and record the margin. If the margin is under 2 GB, the owner report names the ImprovedDb items on node-main that would free it, because books alone uses about 3 GB there.
3. Write the owner report for what this plan does not change, and for what remains after D12 and D13:
   - ImprovedDb on node-main: data, archives, staged operations, images, manual-only host cleanup;
   - Portfolio's build cache and images;
   - CarbonCalculator's images on node-demo;
   - journald caps;
   - NuGet caches.

   Give each a size, a safe owner action and its risk. Put it in the PR description and the final report.
4. Mark P16–P21 and D11–D13 Complete, then set the status line.

#### P21 — Bound system logs on every node

1. The template's node setup sets a journald drop-in `/etc/systemd/journald.conf.d/50-grumplates-size.conf` with `SystemMaxUse=1G` and `SystemKeepFree=5G`. Use the Common role every setup playbook already applies, for both LocalSingleNode and LocalCluster nodes. Changing the setting needs a `systemctl restart systemd-journald` handler; nothing else restarts.
2. Tests: the rendered templates and playbook syntax checks cover the new file. Note it in the setup docs.
3. Focused tests, full local gate, PR, merge, main CI green.
4. Apply it once on all six nodes with the same file and handler (owner grant of 2026-11-04). Record `journalctl --disk-usage` before and after. Journald then trims old entries itself.

#### D11 — Port P16 downstream (CarbonCalculator, Portfolio)

After P16 merges, port its guard change and tests to `Deployment/LocalSingleNode/Scripts/lib/maintenance.py` in CarbonCalculator and Portfolio. Use one PR each and run each repository's full gate (CarbonCalculator: `Validate.ps1 -Mode full`). Merge when `build-test-push` is green.

#### D12 — Automatic cleanup downstream (Portfolio, ImprovedDb, CarbonCalculator)

Each repository cleans up after itself through its own scoped code, the way P17 and P19 make the template do:

1. Portfolio: its `localsinglenode-maintenance.yml` `maintenance` job runs only on dispatch. Schedule it daily, matching the template's schedule pattern and keeping its scoped mode.
2. ImprovedDb: its `localcluster-docker-maintenance.yml` `prune` job runs only on dispatch. Schedule it daily, in its owned-only mode (its AGENTS.md: scoped cleanup scripts only; preserve Books, LocalData, backups, production and unowned resources; never prune volumes).
3. CarbonCalculator and Portfolio: port the P19 build-cache fix wherever they carry the same `build_cache.py` or schedule.
4. Use one PR per repository, run each repository's full gate, and merge when green. Verify each repository's first scheduled or dispatched run, and record the node's free space before and after.

#### D13 — ImprovedDb on node-main: find and fix what keeps its images and files

node-main keeps 78 ImprovedDb app images, 100 release-payload images, 16 GB of `maintenance-volume-archives`, 4.7 GB of `staged-operations` and 76 stopped unlabelled test containers.

1. Read ImprovedDb's retention code (`prune-docker-residue.sh`, `prune-release-residue.py`, `prune-ci-residue.py`, `protected-release-images.sh`, and the archive and staged-operation writers). Then dry-run its scoped scripts (`--dry-run`) on node-main to see what each would remove and what it skips, and why. Look for the P16 tag-count cause, ownership labels that no longer match, and missing age limits.
2. Fix each cause in ImprovedDb's code with tests (one PR per cause, full gate, green `build-test-push`). Add bounded retention, with an age and count floor, for archives and staged operations only where the code proves they are finished and not needed for recovery. Never touch `LocalData` or backups.
3. Run ImprovedDb's maintenance workflow under its own lock to apply the fixes. Record free space before and after. ImprovedDb Dependabot #329 fails on node-main disk pressure, so rerun it afterwards and merge it once green.

#### P22 — Remove old release payload images on node-main (added 2026-11-04, Q20)

A new stage of the LocalCluster maintenance, `Deployment/LocalCluster/Scripts/prune-payload-images.py`, removes this app's payload images when all of these hold:

- older than 24 hours;
- every reference in this app's payload repository;
- the app-image label matches;
- unused.

It rechecks each image and removes it by ID without force, under the deployment lock.

#### P19b — Pass the BuildKit budget in bytes (added 2026-11-04, Q22)

`build_cache.prune_command` passes `keep_gb * 1000**3` bytes. Docker reads "20GB" as GiB, while `docker system df` reports decimal GB.

#### P23 — Remove this repository's old untagged images (added 2026-11-04)

A rebuild moves the tag and leaves the old image untagged. `maintenance.candidates()` now removes an untagged image when its labels prove it is this repository's, under the same age and container rules. Foreign and unlabelled untagged images stay report-only.

#### P24 — Remove old app images that exist only as a digest (added 2026-11-04, Q25)

LocalCluster app servers pull releases by digest, so Docker 29 lists them with tag `<none>`, and `prune_old_unprotected_localcluster_images` skipped them all. Untagged rows of the app repository are now handled by their `repo@sha256:…` reference under the same rules. Rows without a digest, and other repositories, are still skipped.

#### P25 — A payload image deleted by an earlier untag counts as removed (added 2026-11-04, Q27)

`remove_payload_images` re-inspects an image after a failed untag. If the image is gone, it counts as removed; a real conflict is still refused. This ports CarbonCalculator `d0bb8eb4`.

#### D14 — Port P23 downstream (CarbonCalculator, Portfolio)

#### D15 — Portfolio: release payload image retention (Q25)

Port the template's LocalSingleNode `payload_candidates` and `remove_payload_images` to Portfolio's `maintenance.py`, with tests.

#### D16 — Port P25 to Portfolio (Q27)

For D14–D16: one PR per repository, the repository's full gate (CarbonCalculator: checked affected validation, Q23), and merge when green. Verify on a maintenance run.

## 7. Downstream repositories (packets D01-D10)

These repositories were created from this template (except JobHunt) and have since diverged. Port only the small, proven fixes below (OD-16). Do not refactor them otherwise.

| Repo | GitHub | Local checkout | Notes |
| --- | --- | --- | --- |
| CarbonCalculator | `Grumlebob/CarbonCalculator` | `C:\Users\jgrum\Documents\Programming\Csharp\CarbonCalculator` | Live on `node-demo`. Accounts on. Runner routing **enabled** (desktop runner). |
| ImprovedDb | `Grumlebob/ImprovedDb` | `C:\Users\jgrum\Documents\Programming\Csharp\ImprovedDb` | Keeps `BlazorAutoApp` names. Its own runner routing and release design. **The primary checkout has uncommitted work: never touch it.** |
| Portfolio | `Grumlebob/Portfolio` | `C:\Users\jgrum\Documents\Programming\Csharp\Portfolio` | Live on `node-portfolio`. Node 24 LTS on purpose (its `docs/Frontend.md`). |
| JobHunt | `Grumlebob/JobHunt` | `C:\Users\jgrum\Documents\Programming\JobHunt` | Documents only, not from the template. Branch hygiene only. |

### 7.1 Rules for downstream packets

- That repository's `AGENTS.md` and `docs/Test.md` override this plan. Read them before its first packet. Their gates:
  - CarbonCalculator: `pwsh -File Scripts/Testing/Validate.ps1 -Mode affected -Base origin/main` before pushing. Use `-Mode full` in its prepared Linux environment when its `AGENTS.md` asks for it.
  - Portfolio: the local gate in its `docs/Test.md` (short form in its `AGENTS.md`, including `bash Deployment/Common/Scripts/audit-deployment.sh`). On Windows some Python suites assume Linux; compare with a clean `origin/main` worktree before treating a failure as yours.
  - ImprovedDb: its `docs/Test.md` gate. Merge code PRs with a reviewed message and no skip-CI marker: `gh pr merge <n> -R Grumlebob/ImprovedDb --squash --subject '<subject>' --body-file <file>`.
  - `repo-gate` from Git Bash works in any of these repositories.
- Work in a new worktree: `git -C <checkout> fetch origin --prune`, then `git -C <checkout> worktree add ../<Repo>-lean origin/main --detach`, then `git switch -c chore/template-port-<packet>-<yyyymmdd>` inside it. Remove the worktree after merge.
- Port by reading, not by copying blindly: `gh pr diff <n> -R Grumlebob/GrumPlates` (or the merged template commit), then apply the same change with that repository's names and paths.
- One PR per packet per repository. Merge only when `build-test-push` is green on the exact PR head. Pass `-R Grumlebob/<Repo>` to every `gh` command.
- **No downstream CD dispatch and no host changes.** Read-only `ssh` listing is allowed only where a packet says so.
- A Dependabot PR is unblocked by merging the fix, then commenting `@dependabot rebase`. Merge it when green on its exact head, unless the repository's Dependabot auto-merge workflow does it first. If Dependabot closes or replaces it, handle the replacement and record the new number.

### 7.2 Facts (2026-11-02, read-only)

| Repo | `origin/main` | Open PRs | Why they fail |
| --- | --- | --- | --- |
| CarbonCalculator | `e0db25e2` | #341 (GitHub Actions bump in `codeql.yml`) | Green and `CLEAN`; not merged. |
| ImprovedDb | `853f7c9a` | #426 (Lighthouse 13.6.0 in `BlazorAutoApp.Client`), #329 (Actions bump) | #426: npm audit, `minimatch` 10.2.6 via `@sentry/bundler-plugins` → `glob`. #329: `Scripts/CI/tests/test_ci_workflow_routing.py:601` asserts the exact SHA `actions/create-github-app-token@bcd2ba49218906704ab6c1aa796996da409d3eb1`, so every bump of that action fails. |
| Portfolio | `8cced2c` | #197 (Node 24 → 26), #198, #199 (.NET image digests), #200 (Actions), #201 (Lighthouse in `Scripts/Testing/BrowserTools`), #202 (NuGet) | #198-#202: "Audit React frontend dependencies". `npm audit --package-lock-only` in `Portfolio.React` shows 14 moderate, all through `minimatch` (eslint, `@typescript-eslint/typescript-estree`, `@eslint/config-array`, `openapi-typescript` → `@redocly/openapi-core`), the same tree GrumPlates #409 fixed. #201 also has the Lighthouse `glob` path. #197: the deployment audit pins the literal `FROM node:24.22.1-bookworm-slim`. |
| JobHunt | `a7cb015` | none | — |

- CarbonCalculator already ported #409 (its #426, `4c7ac0b0`); its React audit is clean. Its `Scripts/Testing` still uses Lighthouse 13.5.0 (no `glob` yet).
- Node pins. CarbonCalculator: `26.12.1` in `CarbonCalculator/Dockerfile:3`, `CarbonCalculator.React/package.json:8`, `.github/workflows/ci.yml:420,494` (re-find others with `git grep -n '26\.12\.1'`). Portfolio: `24.22.1` in `Portfolio/Dockerfile:3`, `Portfolio.React/package.json:8`, `Scripts/Testing/BrowserTools/package.json:6`, `.github/workflows/ci.yml:260,285`, `Deployment/Common/Scripts/Component/lib/audit_deployment.py:410,498`, `Scripts/Testing/RunReactPublicE2E.ps1:84-101`, `docs/Frontend.md:62`. `Scripts/Quality/tool-versions.json` pins Node for the optional Sonar tooling; leave it.
- Template leftovers (hits outside `Plans/`): `settings.defaults.json` and `AppConfigurationExtensions` exist in all three apps. CarbonCalculator: legacy Data Protection import (61 hits, 16 files; inventory `data_protection_legacy_key_store: None`, `accounts_enabled: true`) and legacy release path (92 hits, 22 files, including `pin-localsinglenode-legacy-release.yml`). Portfolio: legacy release path (44 hits, 14 files). The latest successful CD runs used `RELEASE_PAYLOAD_TRANSPORT: ghcr-container` (CarbonCalculator run 41322421370 on 2026-11-01; Portfolio run 41092492054 on 2026-10-30).
- Extra branches. JobHunt: `codex/portfolio-aware-applications` (PR #1 merged; one commit not on `main` by ancestry). CarbonCalculator: `backup/codex/p18-ac26-upload-constraints` (5 commits), `backup/codex/template-trx-run-summary` (6), `backup/codex/third-consumption-conflict-review-20261022` (1), `backup/codex/third-five-consumption-boundaries-20261022` (1), `task/test-feedback-review-20261027` (2; PR #388 closed). None has an open PR.

### D01 — Portfolio: port the React minimatch fix (#409) and the Lighthouse override

1. Read `gh pr diff 409 -R Grumlebob/GrumPlates` and CarbonCalculator's port `git -C <CarbonCalculator checkout> show 4c7ac0b0`.
2. Copy `GrumPlates.React/scripts/patch-redocly-minimatch.mjs` and `patch-redocly-minimatch.test.mjs` into `Portfolio.React/scripts/`. Change only names and paths.
3. `Portfolio.React/package.json`: add the same `postinstall` script and `overrides` block as `GrumPlates.React/package.json` on `main`. First run `npm ls @redocly/openapi-core` in `Portfolio.React`. If its version is not 1.34.x, keep Portfolio's version and override only `minimatch`.
4. `Portfolio/Dockerfile` `react-dependencies` stage: `COPY` the patch script before `npm ci`, as `GrumPlates/Dockerfile` does. Update the Portfolio audit rule that asserts that stage (~498) in the same PR.
5. The patch test runs under Vitest. GrumPlates' `vitest.config.ts` includes `"scripts/**/*.test.mjs"`; Portfolio's includes only `app/**/*.test.{ts,tsx}`. Add `"scripts/**/*.test.mjs"` to Portfolio's `include`. The test also checks the Dockerfile `COPY` line, so adjust its expected path to `Portfolio.React/scripts/...`.
6. `Scripts/Testing/BrowserTools/package.json`: add `"overrides": { "glob": { "minimatch": "11.0.1" } }` (merge into any existing `overrides`).
7. In both folders: `npm install --package-lock-only`, `npm ci`, `npm audit --audit-level=moderate` (no findings). In `Portfolio.React` also `npm run lint`, `npm test`, `npm run build`, `npm run api:check`. In BrowserTools, `npx lighthouse --version` must work.
8. Portfolio local gate, PR, merge. Then `@dependabot rebase` on #198, #199, #200, #201 and #202 and merge each when green.

### D02 — Lighthouse `glob` override in ImprovedDb and CarbonCalculator

Same fix as template step P01-A 3a.
1. ImprovedDb `BlazorAutoApp.Client/package.json` and CarbonCalculator `Scripts/Testing/package.json`: add `"glob": { "minimatch": "11.0.1" }` to `overrides` (create the object if missing; keep existing entries). In CarbonCalculator it is inert until Lighthouse 13.6.0 arrives, so its next Dependabot PR does not fail.
2. In each folder: `npm install --package-lock-only`, `npm ci`, `npm audit --audit-level=moderate`, and the folder's build script (ImprovedDb: the Tailwind CSS build named in its `package.json`).
3. One PR per repository, each with its own gate. After the ImprovedDb merge, `@dependabot rebase` on ImprovedDb #426.

### D03 — ImprovedDb: stop pinning an action SHA in a test

1. In `Scripts/CI/tests/test_ci_workflow_routing.py` (~601) replace the literal assertion with `self.assertRegex(SELECT_RUNNER_JOB, r"actions/create-github-app-token@[0-9a-f]{40}\b")`. The regex keeps the test's meaning (the job uses that action, pinned to a full SHA) without breaking on every Dependabot bump.
2. `git grep -n -E '@[0-9a-f]{40}' -- Scripts/CI/tests` and convert every other literal SHA that only repeats a workflow pin in the same way.
3. Run that test module, then ImprovedDb's gate. PR, merge with a reviewed message (7.1). Then `@dependabot rebase` on #329.

### D04 — CarbonCalculator: merge #341

`gh pr checks 341 -R Grumlebob/CarbonCalculator`. If `build-test-push` is green on the current head, `gh pr merge 341 -R Grumlebob/CarbonCalculator --squash --delete-branch`. Otherwise comment `@dependabot rebase` and merge when green.

### D05 — Portfolio: stay on Node 24 and use one Node version source (OD-17)

1. Close #197: `gh pr close 197 -R Grumlebob/Portfolio --comment "Portfolio stays on Node 24 LTS (docs/Frontend.md). Node major updates are ignored by Dependabot; patch updates still arrive."`
2. `.github/dependabot.yml`, the `docker` block for `directory: "/Portfolio"`: add
   ```yaml
       ignore:
         - dependency-name: "node"
           update-types: ["version-update:semver-major"]
   ```
3. Apply template packet P08's design with Portfolio's files (7.2 lists them). The source of truth is the `FROM node:` line in `Portfolio/Dockerfile`. Both `engines.node` become `">=24.22.1 <25"`. `ci.yml` derives the version; the two audit literals become one rule that `ci.yml` derives it from the Dockerfile and that the Dockerfile stage is named `react-dependencies`. `RunReactPublicE2E.ps1` reads the Dockerfile. `docs/Frontend.md` says "the version in `Portfolio/Dockerfile`". Leave `Scripts/Quality/tool-versions.json`.
4. Verify: `git grep -n '24\.22\.1' -- ':!Plans' ':!*package-lock.json' ':!Scripts/Quality'` shows only the Dockerfile and the two engine ranges. Portfolio local gate, PR, merge.

### D06 — CarbonCalculator: one Node version source (after template P08)

Port template P08's merged diff to CarbonCalculator (7.2 lists its pins). `engines.node` becomes `">=26.12.1 <27"`. Verify with `git grep -n '26\.12\.1' -- ':!Plans' ':!*package-lock.json'`, then its gate, PR, merge.

### D07 — Settings tidy (after template P09), CarbonCalculator, ImprovedDb and Portfolio

Port only template P09 steps 1 and 2: delete `settings.defaults.json` (moving any unique keys into `appsettings.json`) and the configuration-source re-adding code (`AppConfigurationExtensions` or its equivalent), with the same checks as P09. One PR per repository. If a repository's version differs enough that the port is no longer a small change, skip it there and record why.

### D08 — CarbonCalculator: remove the legacy Data Protection import (after template P02)

Port template P02's merged diff. CarbonCalculator already has accounts on, so its keys are already in PostgreSQL with a certificate; only the import and re-wrap code, settings, Ansible tasks, templates, tests and docs go. Verify with P02 step 8's grep. Gate, PR, merge.

### D09 — Remove the legacy release path (after template P04), CarbonCalculator and Portfolio

1. Re-check read-only, per repository: the newest successful CD run's log shows `RELEASE_PAYLOAD_TRANSPORT: ghcr-container`. List the host pin store read-only: find its path from the repository's LocalSingleNode settings (`deploy_root` + `release-payloads/`, as in template section 4.2), then `ssh node-demo 'sudo -n ls <path>'` (CarbonCalculator) or `ssh node-portfolio 'sudo -n ls <path>'` (Portfolio). If any `legacy-` pin or `actions-artifact` use shows up, skip that repository and report.
2. Port template P04's merged diff, including the `pin-localsinglenode-legacy-release.yml` workflow in CarbonCalculator. Verify with P04 step 13's grep. Gate, PR, merge.
3. Do not deploy. The owner's next normal deploy is the first live use; say so in the final report.

### D10 — Downstream branch hygiene

For each branch in 7.2:
1. `git -C <checkout> fetch origin --prune`. Changed files: `files=$(git diff --name-only origin/main...origin/<branch>)`.
2. It is integrated when `git cherry origin/main origin/<branch>` prints only `-` lines, **or** `git diff origin/main origin/<branch> -- $files` is empty, **or** every hunk of `git diff origin/main...origin/<branch>` is already present on `main`. Check that last one by reading.
3. Integrated: `git push origin --delete <branch>`, delete any local copy, and record the full SHA. JobHunt's branch is expected to be integrated (PR #1 merged).
4. Not integrated: keep it. Record a one-line summary of what it holds for the owner. Never delete it.
5. Never touch any other branch, worktree or checkout.

### 7.3 Not ported (OD-18)

Runner-routing removal (CarbonCalculator uses routing; ImprovedDb has its own). Migration squash (live data on shared hosts, no runtime gain). `refresh_rating_ranks` (a real ImprovedDb feature). Removing the accounts switch and replacing `LocalAccounts` in CarbonCalculator or ImprovedDb (major refactors). ImprovedDb's release path (a different design). Markdown-reading audit rules downstream. The owner can ask for any of these later.

## 8. Out of scope

- Quality tools (SonarQube, Qlty, Lighthouse) stay. They are optional and not merge gates.
- Package upgrades beyond the Dependabot PRs, framework changes and Book feature redesigns.
- The owner's live infrastructure files that the template uses to deploy its own demo (`Deployment/LocalCluster/inventory/prod/vault.yml`, `hosts.yml`, the tracked `group_vars`). Forks replace them per `docs/HowToForkThisRepo.md`.
- `Plans/CloudflaredConnectorFollowUps.md` work (connector pin bump etc.). It is a separate open plan.
- Q-002 (Caddy MD5 check), Q-003, Q-004 in `Plans/CodeQualityImprovements.md`.
- Downstream work beyond packets D01-D13 (OD-18, section 7.3).
- Application data (`/opt/improveddb/LocalData`, databases, volumes, backups) and `daemon.json`. P20 reports them to the owner with sizes and safe actions. NuGet and npm caches stay, because CI reuses them; P20 reports their size.

## 9. Risks and how to handle them

| Risk | Handling |
| --- | --- |
| A test silently depended on accounts being off | P03-A turns accounts on in the test factory first, in its own PR, and lists every changed expectation. |
| Removing the Data Protection import loses keys on a live host | Section 4.2: every inventory uses `None`, so nothing was ever imported. Keys move from Redis to PostgreSQL in P14, which only ends sessions. |
| A live target still runs a legacy-pinned release | P04 step 1 re-verifies before removing the code. |
| `require_ci_results.py` rejects the new job set after P05 | P05 step 7 removes the selection inputs in the same PR. Verify on the PR's own CI. |
| Consolidated unittest discovery runs fewer tests | P10 step 6 compares test totals between old and new runs. |
| The audit loses a check that mattered | P11 deletes only Markdown-reading rules. Every code, template and workflow rule stays. |
| The squashed migration differs from the live schema | P01-B brings LocalCluster to all four migrations. P07 step 4 requires identical schema dumps from old and new migrations. P14 step 3 diffs each live schema against the new one, takes a verified backup first, and the rewrite SQL aborts unless exactly the four old IDs are present. |
| A CD run with migrations between P07 and P14 | Section 5 forbids it. If it happens anyway, the bundle fails on existing tables before changing anything; fix by running P14 step 3 for that database. |
| A downstream port breaks a live product | Only small, template-proven changes (OD-16); each repository's own gate and `build-test-push`; no downstream deploys. D09 checks pin stores read-only first and skips a repository on any doubt. |
| Seeded passwords leak | They are generated into the ignored `.env`, never printed, never logged, and never stored in tracked files. Deployments do not get a User seed. |
| Developers and CI without a key-encryption certificate | Docker local setup creates `docker/https/aspnetapp.pfx`; Windows Development uses DPAPI; Linux/macOS Development falls back to unencrypted keys with a warning (OD-10); `ci-docker-smoke.sh` generates a certificate (P03-B step 10); deployments mount one from the vault. |
| Existing developer `.env` files lack seeded accounts | `setup-local.ps1` fills unconfigured seeds in place (OD-12). |
| Disk cleanup removes something another app or a rollback needs | Only books-owned resources (P16–P18 rules). No forced removal, no hand-typed prunes, no volumes. Current, recovery and pinned images are skipped and rechecked right before each removal. Removed images can be pulled again from GHCR. |
| Daily LocalCluster cleanup (P17) disturbs ImprovedDb on node-main | The scheduled mode is the existing owned-only mode under the shared deployment lock. P17 step 5 compares ImprovedDb's container and image counts before and after. |

## 10. Manual blockers (owner)

None block execution. Starting [LeanTemplateCleanupGoal.md](LeanTemplateCleanupGoal.md) is the authorisation for P01-B and P14 (backups, history rewrites, CD dispatches, release status), for deleting the stale branch and the unused repository variable, and for the downstream packets: opening and merging their PRs, merging the unblocked Dependabot PRs, closing Portfolio #197, read-only pin-store listing in D09, and deleting downstream branches that D10 proves integrated. Remove that paragraph from the goal before starting it if you want to do those steps yourself.

Optional, owner only: `agent.md` in the repo root is your private, ignored notes file with outdated SSH details and Codex instructions. Agents leave it alone.

## 11. Questions answered by the owner (2026-11-02)

| ID | Question | Answer |
| --- | --- | --- |
| Q1 | With accounts always on, registration needs SMTP. How do local development and Blazor E2E get users? | Seeded accounts, no local mail server (OD-6; P03-C, P03-E). |
| Q2 | Squash the four EF migrations? | Yes (OD-7; P07). The plan keeps live data by rewriting migration history (P14 step 3) instead of recreating databases. |
| Q3 | Remove the never-enabled CI runner profile routing? | Yes (OD-8; P05). |
| Q4 | Remove deployment-audit rules that read Markdown? | Yes (OD-9; P11). |
| Q5 | Lighthouse 13.6.0 also fails audit on `glob` itself (GHSA-3g3m-2fjr-6pw4, glob 9.0.0-14.0.1, published 2026-10-30), so the `glob` → `minimatch` override in P01-A 3a, D01 step 6 and D02 is not enough. | Executor, 2026-11-02: override `"@sentry/bundler-plugins": { "glob": "14.0.3" }` instead. glob 14.0.3 depends on `minimatch ^11.0.2`, so one scoped override fixes both advisories. Verified with Lighthouse 13.6.0: audit clean, `lighthouse --version` runs, and the plugin's CJS and ESM entry points load on Node 26. Downstream packets use the same override. |
| Q6 | P03-B deletes `RateLimiting:Authentication`, but local tooling raised that limit so browser tests could register and sign in repeatedly. What replaces it? | Executor, 2026-11-02: `docker-compose.yml`, `RunLocal.ps1`, `docker/local-status.py` and `docs/Testing/Browser.md` now raise `Accounts__WriteLimit__PermitLimit`, the limit that governs account POSTs once accounts are always on. No new setting. |
| Q7 | The packaged React "accounts disabled" browser test (`Category=ReactPackagedDisabled`) and its smoke run cannot pass once accounts are always on. | Executor, 2026-11-02: P03-B deletes that test and smoke run and updates the two audit needles that named it; P03-D does the rest of the smoke simplification. The audit's fixed "three TRX result directories" count became "every smoke `--results-directory` is under the job report path", which checks every run instead of a number. |
| Q8 | `RunLighthouse.ps1 -AuthenticatedLocalUser` signed in with the removed demo login. What does it use now? | Executor, 2026-11-02 (P03-C): it reads the seeded User from `.env` when `-Email`/`-Password` are not given. The empty `SeedFrontendDataAsync` hook is removed. `setup-local` writes `.env` with LF line endings, because a raw Compose `env_file` would keep a CR in the password. The demo-login audit rules became "no `UserSeed` in deployment compose or `app.env` templates". The `Test-DeployedSite` check that `admin@admin.com` cannot sign in is kept: it proves default credentials are refused. |
| Q9 | With accounts always on, `AccountPrincipalGuard` also applies to Blazor Books. The default `AddAuthenticationStateSerialization` sends only name and role claims, so WebAssembly had no user ID for `X-Expected-User` and Books returned 409 ("Your bookcase could not be loaded"). Found by the P03-E browser tests. | Executor, 2026-11-03: a custom `SerializationCallback` (`AccountAuthenticationStateSerialization`) also sends `NameIdentifier`; the security stamp and other claims stay on the server. Leaner and safer than `SerializeAllClaims`. Fixed in P03-B with unit tests. |
| Q10 | P04 removes Actions-artifact releases, but ImprovedDb's shared LocalCluster coordinator (`Scripts/DeployLocalCluster.ps1`) always sends `artifact_id` and `artifact_digest` when it dispatches `cd-localcluster`, and GitHub rejects undeclared inputs. | Executor, 2026-11-03: keep both inputs, documented as "Must be empty"; the workflow fails when either is set. The coordinator keeps working and no Actions-artifact path remains. |
| Q11 | P04 step 1 found an orphan payload folder on node-main, `/opt/books/release-payloads/12008462956-…` (an `artifact.zip` from 2026-10-19), outside the per-repo pin root. No release marker on node-main, node-app1, node-app2 or node-demo refers to it; every marker is GHCR schema 2. | Executor, 2026-11-03: leave it in place, because host deletion is not authorised. The final report lists it for the owner. |
| Q12 | With accounts always on, the deployed Blazor acceptance in `Scripts/Test-DeployedSite.ps1` can no longer register, sign in and delete a temporary account. Every target keeps registration off, registration needs email confirmation, and over plain LAN HTTP the account forms cannot render, because their antiforgery cookie is Secure. Found by the P03-B image gate. | Executor, 2026-11-03 (P03-B): the Blazor acceptance never creates an account. It checks the home page and the anonymous sign-in redirect everywhere. On HTTPS origins (real or forwarded) it also checks the protected sign-in form; on real HTTPS it checks the Secure, HttpOnly account cookie and that the default `admin@admin.com` login is refused. Registration stays covered by integration tests (OD-6). The image smoke trusts only its network gateway as the forwarding proxy and sends `X-Forwarded-Proto: https` for Blazor, as it already did for React. Account sign-in over plain LAN HTTP is not supported; use the public HTTPS hostname. |
| Q13 | P06 step 5a deletes the `refresh_rating_ranks` input of `cd-localcluster.yml`, but ImprovedDb's shared LocalCluster coordinator requires that input in the workflow contract it checks and always sends it. | Executor, 2026-11-03 (P06): keep the input, documented as "Must be false"; the workflow still refuses `true`. Removing it would break coordinated LocalCluster deploys. Same reason as Q10. |
| Q14 | P03 is planned as five sub-PRs, but after P03-B no intermediate state passes CI: Blazor E2E can sign in only after P03-E switches it to the seeded User, which needs P03-C's seed. | Executor, 2026-11-03: P03-B to P03-E ship as one PR that keeps each sub-packet as its own commit; the ledger rows for P03-B to P03-E all point to it. The image smoke runs the Blazor browser tests against a second HTTPS container with a per-run User seed, because account cookies are Secure. |
| Q15 | P07: the plan's step-7 search `2026052[0-9]{6}_` is one digit short and misses the old IDs, and the step-4 normaliser `^\(un)?restrict ` keeps the `\restrict` lines when run from Git Bash. What do we use? | Executor, 2026-11-03: `202605[0-9]{8}_\|20261015183858` for step 7, and `^.(un)?restrict ` for step 4. With these the step-7 search returns nothing and the normalised dumps are byte-identical. |
| Q16 | P07: `AccountMigrationTests.DuplicateOrOversizedNormalizedEmails_RefuseUpgradeWithoutChangingAccounts` tests the duplicate-email guard in the deleted `AccountOutboxAndDataProtectionKeys` migration. `InitialSchema` ports no raw SQL. Keep it? | Executor, 2026-11-03: deleted with the guard. A fresh database gets the unique email index at creation, which the remaining migration test checks. Live databases already passed the guard. |
| Q17 | P15 step 7 says to delete the memory note `docs-skip-ci-can-break-audit.md` because P11 made it obsolete. P11 changed only the template; CarbonCalculator and Portfolio keep Markdown-reading audit rules (OD-18, section 7.3), so the note is still true there. | Executor, 2026-11-04: keep the note and narrow it to the downstream repositories, with the date P11 removed the rules from the template. Deleting it would drop a rule that still prevents broken downstream `main` builds. |
| Q18 | P14 step 4: CD for the additional React instance (run 41777872049) failed only `React-enabled-session` and `React-enabled-session-cookie` on the LAN `http://` URL. Accounts are always on, and the session's `__Host-CSRF-*` antiforgery cookie is Secure, so the server cannot set it over plain HTTP. This is the React counterpart of Q12. P03-D step 3 said to always run `Invoke-ReactAccountSessionAcceptance`. | Executor, 2026-11-04: run the React session checks only on HTTPS or with `-ForwardedProto https` (same gate as Q12). Plain-HTTP runs still check the client config, post-only routes, unknown-API 404 and the cookie-free public pages. Tests cover both paths. Then redeploy every target at the new green main SHA. |
| Q19 | P14 step 4.3: the LocalCluster CD at `9bb714ee` (run 41783180973) stopped at "Check node-main capacity". `/opt` on node-main had 19892 MiB free against a 20480 MiB reserve. node-main's root disk is 92% used: `/opt/improveddb` 88 GB, Docker images 47.7 GB (38.7 GB reclaimable) and build cache 26 GB. Books uses about 3 GB. Freeing space would need pruning or deleting other projects' resources, which the goal forbids. | Executor, 2026-11-04: stop this target and report, as the goal requires when a guard fails. The guard is not lowered. LocalCluster keeps its P01-B release `54871c53`; its history is already rewritten, and the app does not read it. The P14 window has closed, so a later deploy with `run_migrations=true` is safe and applies nothing. Owner: free about 1 GB on node-main `/opt`, for example ImprovedDb's unused images or build cache, then run `gh workflow run cd-localcluster.yml -R Grumlebob/GrumPlates --ref main -f run_migrations=true -f target_sha=<green main SHA>` and `release-status.yml -f target=localcluster -f app_instance=primary`. Owner, 2026-11-04: approved removing old books images and the Q11 orphan folder. Removed the books app images `d28a6c7b` and `9cb1961f`, the payload images of those two releases and of `a5bd0287`, and `/opt/books/release-payloads/12008462956-…` (61 MB). Nothing was force-removed. The pin folders, the live and recovery images, and the running `books-ci-*-38004425567-1` containers (and their `a5bd0287` image) are untouched. `/opt` free space rose from 20094 to 20919 MiB, then the redeploy above succeeded. |
| Q20 | The ImprovedDb maintenance dispatch (run 41799134593) removed 20 app images but left 100 release payload images (26.8 GB unique). A deployment needs its payload image only while it copies `/release` into a file pin, and nothing removed it afterwards: not in the template on LocalCluster, nor in ImprovedDb. | Executor, 2026-11-04: new packet P22 adds `prune-payload-images.py` to the template's LocalCluster maintenance (this app's payload repository and label only, older than 24 h, unused, non-forced removal after a recheck, under the deployment lock). D13 ports it to ImprovedDb (#439; dry run: 92 candidates). |
| Q21 | D13 step 2: may `maintenance-volume-archives` (16 GB) and the 76 unlabelled stopped Testcontainers (58 anonymous postgres volumes, about 2.4 GB) on node-main be removed? | Executor, 2026-11-04: no. ImprovedDb's documented policy keeps "legacy PostgreSQL recovery archives: protected; no automatic expiration", so expiring them is an owner decision. The containers all come from 9 Testcontainers sessions on 2026-10-08 (Testcontainers .NET 4.13.0) before ImprovedDb labelled its test containers (675bf5e8, same day). Three repositories run Testcontainers there, so ownership cannot be proven and the rules keep them. Labelled leftovers are swept by `prune-ci-residue.py`. Both go into the P20 owner report. |
| Q22 | P19 step 4: the first verification run (41828469044) trimmed the cache's own records to 21.26 GB against a 20 GB budget, and a second run trimmed nothing. | Executor, 2026-11-04: Docker's prune flags read "20GB" as GiB (21.47 GB), while `docker system df` prints decimal GB. New follow-up P19b (#440) passes plain bytes. Verified by run 41849823525: 22.37 → 19.88 GB own. Ported to Portfolio #212 and CarbonCalculator #457. |
| Q23 | D11/D12/D14 CarbonCalculator: `Validate.ps1 -Mode full` hung on this PC for environment reasons (routing tests timing out; later an idle test host). Which gate counts? | Executor, 2026-11-04: CarbonCalculator's docs/Test.md requires checked affected validation (`-Mode affected -Base origin/main`) before pushing non-Markdown changes. Used that, plus green `build-test-push` on the exact head. The hang was Docker Desktop's random published port refusing WSL `127.0.0.1` but accepting `host.docker.internal`, as MACHINE.md records. The gate environment now sets `TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal`. |
| Q24 | CarbonCalculator #457 CI failed twice on runner `node-demo-carboncalculator`: two ReactPackagedAccountsE2ETests failed with Playwright "Protocol error (Network.getResponseBody): No data found for resource" in `Context.Tracing.StopAsync` (E2ETestBase.cs:381). The third attempt picked `carboncalculator-ci-desktop` and passed. | Executor, 2026-11-04: CarbonCalculator's own runner/test issue, outside this plan. Recorded in the owner report. |
| Q25 | P20 measurement found two more leaks the disk packets did not cover. (1) node-app1 and node-app2 each kept every `books` image ever pulled (seven, oldest 2026-10-15), because app servers pull by digest and the retention skipped `<none>` tags. (2) Portfolio kept 66 release payload images, because its maintenance had no payload retention. | Executor, 2026-11-04, under the owner's "thorough, automatic cleanup" grant: new packets P24 (#442) and D15 (Portfolio #214). ImprovedDb has the same loop as P24 but pulls by tag on app servers, so it is not affected and not ported. |
| Q26 | ImprovedDb main CI 41814090682 (`a752d26b`, the Dependabot #329 merge) failed in "Enforce GHCR final-payload capacity through the coordinator": "registry deletion outcome is unknown for … improveddb-release-payload version 1496076969; resume reservation run-41814090682-1". | Executor, 2026-11-04: this is ImprovedDb's GHCR retention state; the step runs after all tests passed. This plan changes no registry state, so it goes to the owner report: resume that reservation with ImprovedDb's coordinator, then rerun main CI. |
| Q27 | Portfolio maintenance 41866879296 (D15 verification) removed all 60 payload images older than 24 hours, yet failed with 22 refusals. Each said "No such image" for the digest entry that Docker 29 lists in `RepoTags`: removing the tag had already deleted the image. All 22 were confirmed gone. The template's `remove_payload_images` has the same loop. CarbonCalculator fixed it on 2026-10-27 (`d0bb8eb4`). | Executor, 2026-11-04: new packets P25 (GrumPlates #443) and D16 (Portfolio #215) port CarbonCalculator's fix. After a failed untag, re-inspect the image; if it is gone it counts as removed. Verified by Portfolio maintenance 41873704911. |

If execution uncovers a new decision: when one option is clearly leaner and safe, take it, record it here as Q5 onwards with the reason, and continue. Ask the owner only when every option risks live data, security or a supported feature (both frontends, the three deployment targets).

## 12. Ledger

| Item | Status | Evidence |
| --- | --- | --- |
| Research and inventory | Complete | 2026-11-02 at `origin/main` `d28a6c7b3afb0a406a217a5508e103de6227e2f8`; sections 4.1–4.11. |
| Owner answers Q1–Q4 | Complete | 2026-11-02: seeded accounts; squash; remove routing; remove Markdown audit rules (OD-6 to OD-9). |
| Downstream review | Complete | 2026-11-02: OD-16 to OD-18, packets D01-D10; facts in section 7.2. |
| Plan review | Complete | 2026-11-02: OD-10 to OD-15; live databases, pins, images and deploy inputs verified read-only (section 4.2). |
| P00 | Complete | 2026-11-02: base `origin/main` `7ee5192a43ddf1276edca21bbf4439cd98d4c80b`. Open PRs #405, #406, #407 as in 4.1. New branch `quality/test-cleanup-20261102` (`a74fc18d`, own worktree, another session's work, not in this plan): left alone. |
| P01-A | Complete | 2026-11-02/03: `coord/node-inventory-handoff` deleted locally and on GitHub (`c6a59ba3afe8afe4a041400cd088ae9c43dbf4ba`). Client override (Q5) in https://github.com/Grumlebob/GrumPlates/pull/414, head `8adde66b`, full local gate PASS, build-test-push green; merged `5c0fe476`, main CI run 41526721424 success. Dependabot #407 rebased and auto-merged (`a2089a1f`, main CI 41538904179 success). #405 was closed by Dependabot ("updatable in another way"); no replacement PR yet, main keeps StackExchange.Redis 3.4.20. |
| P01-B | Complete | Run 41465965668 (target `d28a6c7b`) failed before the app stopped: the stop play rendered `compose.env.j2` before `data_protection_certificate_gid` was set (bug since #320); app untouched, lock released. Fix https://github.com/Grumlebob/GrumPlates/pull/415 (head `68d79308`, full local gate PASS) merged `54871c53`, main CI 41551063010 success. CD run 41604062400 (`run_migrations=true`, target `54871c539f55d79550aedfcff9c2288a8886420c`) success: pre-migration backup `predeploy-20261103T083555Z.sql.gz`, migration bundle ran on node-db, acceptance ok. `__EFMigrationsHistory`: `20260525172002_InitialTemplateSchema`, `20260526083400_UserOwnedBooks`, `20260528104203_NormalizeBooksForAuthorBooks`, `20261015183858_AccountOutboxAndDataProtectionKeys`. |
| P02 | Complete | https://github.com/Grumlebob/GrumPlates/pull/417 (head `b2b222aa`, full local gate PASS; the BlazorAuto and React account suites were also run alone: 164/164 and 150/150) merged `56264f53`, main CI 41672909266 success. |
| P03-A | Complete | https://github.com/Grumlebob/GrumPlates/pull/418 (head `1b799d6a`; code gated as `4fed45d6`, full local gate PASS) merged `d9efe504`, main CI 41680334445 success. |
| P03-B | Complete | https://github.com/Grumlebob/GrumPlates/pull/419 (one PR for P03-B to P03-E, Q14; head `b835c474`; full local gate PASS on `e2aab6a1` except a stale smoke fixture, fixed and re-gated on `b835c474`) merged `55381b3a`, main CI 41697791997 success. |
| P03-C | Complete | https://github.com/Grumlebob/GrumPlates/pull/419 (one PR for P03-B to P03-E, Q14; head `b835c474`; full local gate PASS on `e2aab6a1` except a stale smoke fixture, fixed and re-gated on `b835c474`) merged `55381b3a`, main CI 41697791997 success. |
| P03-D | Complete | https://github.com/Grumlebob/GrumPlates/pull/419 (one PR for P03-B to P03-E, Q14; head `b835c474`; full local gate PASS on `e2aab6a1` except a stale smoke fixture, fixed and re-gated on `b835c474`) merged `55381b3a`, main CI 41697791997 success. |
| P03-E | Complete | https://github.com/Grumlebob/GrumPlates/pull/419 (one PR for P03-B to P03-E, Q14; head `b835c474`; full local gate PASS on `e2aab6a1` except a stale smoke fixture, fixed and re-gated on `b835c474`) merged `55381b3a`, main CI 41697791997 success. |
| P04 | Complete | https://github.com/Grumlebob/GrumPlates/pull/421 (head `bdee56c4`; full local gate PASS on `7d730456`, same code tree) merged `acc35c79`, main CI 41707778000. |
| P05 | Complete | https://github.com/Grumlebob/GrumPlates/pull/423 (head `7f1d9e26`, exact-head CI 41707891410 success; full local gate PASS on `9c8f732b`, same code tree) merged `03b1b541`, main CI 41713411785. |
| P06 | Complete | https://github.com/Grumlebob/GrumPlates/pull/424 (head `0943e3c6`, exact-head CI 41713555558 success; full local gate PASS on `a890c1e1`, same code tree) merged `28016b14`, main CI 41724047108. P05 follow-up: main CI 41713411785 ran every job on `node-demo-books`; `CI_RUNNER_ROUTING_ENABLED` deleted. |
| P07 | Complete | https://github.com/Grumlebob/GrumPlates/pull/425 (head `3528ec9d`, exact-head CI 41724189780 success; full local gate PASS on `09209238`, same code tree) merged `49098f82`, main CI 41732967439. New migration `20261103193952_InitialSchema`, EF ProductVersion 10.0.14; normalised old and new schema dumps byte-identical (sha256 `ba5dd6a4…cd4032`). No CD with run_migrations=true until P14 step 3. |
| P08 | Complete | https://github.com/Grumlebob/GrumPlates/pull/426 (head `cf8ec9ad`, exact-head CI 41733071880 success; full local gate PASS on `7fae83c3`, same code tree) merged `3612c0ff`, main CI 41741000212. `@dependabot rebase` posted on #406. |
| P09 | Complete | https://github.com/Grumlebob/GrumPlates/pull/427 (head `0035825c`, exact-head CI 41741119162 success; full local gate PASS on `d23e0065`, same code tree) merged `78041da3`, main CI 41750584565. Compose web environment identical before/after (`Plans.local/compose-*.json`). |
| P10 | Complete | https://github.com/Grumlebob/GrumPlates/pull/428 (head `a6756bff`, exact-head CI 41750756153 success; full local gate PASS on `810fa374`, same code tree plus #406) merged `2c4e003c`, main CI 41758536487. Test totals: before 665 tests in 22 invocations (P09 run 41741119162), after 755 in 5 discover steps. |
| P11 | Complete | https://github.com/Grumlebob/GrumPlates/pull/429 (head `c8383118`, exact-head CI 41758602186 success; full local gate PASS on `702df6ab`, same code tree plus #406) merged `af4815ad`, main CI 41763680358. Editing "Reserve LAN IPs" in Deployment/LocalCluster/Setup.md fails the old audit and passes the new one. |
| P12 | Complete | https://github.com/Grumlebob/GrumPlates/pull/430 (head `a50f5e0a`, exact-head CI 41763767567 success; full local gate PASS on `88e3948e`, same code tree before the P11 rebase) merged `9cb1b8c0`, main CI 41767708432. |
| P13 | Complete | Local only, 2026-11-03: deleted bin/obj of GrumPlates, GrumPlates.Client, GrumPlates.Core and GrumPlates.Test, GrumPlates.React/build, GrumPlates/BlazorAutoApp.csproj.user and every __pycache__ under Deployment/ and Scripts/ in the primary checkout. Kept .env, .env.cloud, docker/https/aspnetapp.pfx, Plans.local/, GrumPlates.sln.DotSettings.user, agent.md, Cloud OpenTofu state/plans and inventory hosts.yml. git status clean before and after. |
| P14 | Complete | Backups: LocalSingleNode primary maintenance run 41776808172 (`books-20261104T050336014205Z.dump`), additional maintenance run 41777130963 (`books_react` `…-20261104T050600941249Z.dump`), LocalCluster `/opt/books/backups/predeploy-20261104T050927Z.sql.gz` (verified). Live schemas on node-demo `books`, node-demo `books_react` and node-db `ship` were identical to the P07 `InitialSchema` after normalising (556 lines). The history rewrite on each ran DELETE 4, INSERT 1, COMMIT. Afterwards each history holds only `20261103193952_InitialSchema|10.0.14`; rechecked after the deploys. First deploy target `9cb1b8c0`: primary CD 41777569058 succeeded; additional CD 41777872049 failed only the HTTP account-session checks (Q18, fixed by #431). Final target `9bb714ee` (main CI 41780329967): primary CD 41782631525 success, additional CD 41782920355 success (each acceptance `RESULT: PASS`), LocalCluster CD 41783180973 stopped at "Check node-main capacity" (`free_mb=19892`, reserve 20480) before any change. Release status: primary 41783462040 and additional 41783532075 green, each with current release `9bb714ee`, CI 41780329967, `ghcr-container`, pin verified. LocalCluster 41783639854 green on its current release `54871c53` (P01-B). After the owner-approved cleanup in Q19, LocalCluster CD 41795372056 succeeded at `9bb714ee`. Its capacity check showed `free_mb=20758`, it wrote the pre-deploy backup `/opt/books/backups/predeploy-20261104T075624Z.sql.gz`, and acceptance passed (`https://books.jacobgrum.com/health/ready`). Release status 41796012603 is green. Both app nodes run `9bb714ee`, and `ship` history is still only `InitialSchema`. `Plans.local/squash/live-*.sql` deleted. |
| D01 Portfolio minimatch + Lighthouse | Complete | https://github.com/Grumlebob/Portfolio/pull/204 (head `455eb7d8`, full local gate PASS, adds the Dockerfile audit rule) merged `507ca71`, main CI 41605501343 success. Dependabot #198, #200, #201 (after one rerun of a React E2E timeout) and #202 merged; #199 awaits its rebase. |
| D02 Lighthouse override (ImprovedDb, CarbonCalculator) | Complete | CarbonCalculator https://github.com/Grumlebob/CarbonCalculator/pull/446 (head `0b9d7de6`, full `Validate.ps1 -Mode full` passed; CI rerun 41631672955 green after packaged E2E timeouts under memory pressure) merged `950eda88`, main CI 41646559922 success on attempt 2 (same memory timeout on attempt 1). ImprovedDb https://github.com/Grumlebob/ImprovedDb/pull/434 (head `145404ef`, ImprovedDb's applicable local checks: npm ci, npm audit, css build and a Lighthouse 13.6.0 install proof) merged `9fb48265`, main CI 41656363765 success. ImprovedDb Dependabot #426 (Lighthouse 13.6.0) green on head `dd6a0ec7` and merged `8146f512`. |
| D03 ImprovedDb SHA test | Complete | https://github.com/Grumlebob/ImprovedDb/pull/435 (head `d694fc25`, the workflow unittest suite passed) merged `2384f1e2`, main CI 41656385578 success. `@dependabot rebase` posted on #329; it is merged once green on its rebased head. |
| D04 CarbonCalculator #341 | Complete | https://github.com/Grumlebob/CarbonCalculator/pull/341 merged `96cc443fa5dc13fd95f68e5c16c75c290f661316` (head `ac01ce0d`, run 40234018777 green); main CI 41470049728 success. |
| D05 Portfolio Node 24 single source | Complete | https://github.com/Grumlebob/Portfolio/pull/205 (head `fad89b9`, full local gate PASS) merged `0d01594`, main CI 41653178519 success. Portfolio #197 closed. The Dependabot ignore keeps Node 24: #206 (node 24.22.1 -> 24.22.2, a patch) passed on head `76f275ae` and merged `e25c820b`. repo-gate's react step now takes the Node version from the lower bound of an engines range (user tooling `~/bin/repo-gate.sh`, backup `.bak-20261103`). |
| D06 CarbonCalculator Node single source | Complete | https://github.com/Grumlebob/CarbonCalculator/pull/450 (head `2fa4f392`, exact-head CI 41701608861 success; full local gate passed on `f2886337`, report 759b215d309c4aa4b4706c8fc776a46b) merged `3e37e218`, main CI 41705454939 success. Remaining `26.12.1` hits: the Dockerfile, the `>=26.12.1 <27` range and test fixtures in `Scripts/CI/tests/test_validation_plan.py`. Done by the CarbonCalculator session. |
| D07 Settings tidy downstream | Complete | Portfolio https://github.com/Grumlebob/Portfolio/pull/209 (head `80c6537f`, CI 41714713271, full gate PASS) merged `8440fb11`, main CI 41732330653. ImprovedDb https://github.com/Grumlebob/ImprovedDb/pull/437 (head `17986286`, CI 41713834883, local gate 2194 passed/0 failed) merged `53b4b656`, main CI 41717759781. CarbonCalculator https://github.com/Grumlebob/CarbonCalculator/pull/451 (head `056524e7`, CI 41713854367, full gate report 3e14ec90) merged `02cea28b`, main CI 41725811709 attempt 3 (attempts 1-2 failed on net::ERR_NETWORK_CHANGED from a concurrent local gate on the same engine). |
| D08 CarbonCalculator key import | Complete | https://github.com/Grumlebob/CarbonCalculator/pull/452 (head `da07f71b`, CI 41713863673, full gate report 5a5dfb0f; live `data_protection_legacy_key_store` was `None`) merged `953bcf0b`, main CI 41728322693. |
| D09 Legacy release path downstream | Complete | CarbonCalculator https://github.com/Grumlebob/CarbonCalculator/pull/453 (head `1acbdf6a`, CI 41713877145, full gate report 41812e4e) merged `68dc6ae7`, main CI 41731118170. Portfolio https://github.com/Grumlebob/Portfolio/pull/210 (head `6bd566ff`, CI 41713826033, full gate PASS) merged `47abba45`, main CI 41732625687. Step 1: only `ghcr-` pins on node-demo (6) and node-portfolio (5); latest CD runs 41698966671 and 41092492054 used ghcr-container. Not deployed; first live use is the owner's next deploy. |
| D10 Downstream branches | Complete | Deleted (integrated): JobHunt `codex/portfolio-aware-applications` `6a59bef66f301e17b399f53bda0d424cb853919f` (cherry all `-`, PR #1 merged at that head); CarbonCalculator `task/test-feedback-review-20261027` `8d5f534e3cf46f51b84a081a8210f6272efffd05` on GitHub (merge tree equals main; the local branch is kept because another task's worktree has it checked out). Kept (unique content): `backup/codex/p18-ac26-upload-constraints` `782a007b` (ProjectDocumentUploadLimitTests and P18 docs); `backup/codex/template-trx-run-summary` `de698685` (CI/CD workflow and TRX validation work, 38 files); `backup/codex/third-consumption-conflict-review-20261022` `05255b99` (one review doc); `backup/codex/third-five-consumption-boundaries-20261022` `5c36ce38` (ConsumptionBoundaryAcceptanceTests). |
| P15 | Complete | 2026-11-04 on `origin/main` `9bb714ee` (main CI 41780329967 green). Open GrumPlates PRs: only this closeout PR. `git branch -a`: only `main`, `origin/main`, `origin/HEAD` and the `improveddb/*` remote branches. Task worktrees removed; the primary checkout's `main` was fast-forwarded. Agent memory: `grumplates-rename-plan.md` no longer points to the deleted plan; `docs-skip-ci-can-break-audit.md` kept and narrowed to downstream repos (Q17). Downstream: no open PRs in CarbonCalculator, Portfolio or JobHunt. ImprovedDb has one open PR, Dependabot #329 (GitHub Actions bumps, opened 2026-10-15). D03 asked for a rebase, but its CI still fails on `node-main-improveddb`, with different People storage-pressure tests failing on each attempt (`PeopleStoragePressureException`, bytes below reserve; attempt 3 of run 41745167430). This is the same node-main disk shortage as Q19, so it stays open for the owner. D10 kept branches are listed in the D10 row. Residue search and deleted branches follow. |
| P16 | Complete | https://github.com/Grumlebob/GrumPlates/pull/435 (head `0ebcfba4`, PR CI 41798567044, full local gate PASS on `0ebcfba4`; the profiles step was rerun cleanly after a test host hung once all tests had passed) merged `ba4979b1`, main CI 41819457815 success (it covers #436–#439, whose own main runs were cancelled as superseded). Step 3: `docker image rm` without force removed a throwaway busybox image tagged and pulled by digest on node-demo. Verified by LocalSingleNode maintenance primary 41828469044 (removed a74aede2, fdc6201e) and additional 41828901740 (removed ea3d17a3), 0 refusals. node-demo `/` free 126603 → 131149 MiB; books 22 → 20 and books-react 22 → 20 (all remaining under 48 h). |
| P17 | Complete | https://github.com/Grumlebob/GrumPlates/pull/436 (head `21f14fb4`, PR CI 41798714094, full local gate PASS) merged `84e17b69`. The `prune` job now also runs daily at 03:47 UTC (`47 3 * * *`; artifact retention stays at 03:17) in its owned-only mode. On failure, `check-node-main-capacity.sh` names the maintenance workflow and the Operations.md section to run first. Verified by dispatch 41829760651: all stages 0, only books-owned removals; ImprovedDb images (64 app, 9 payload) and 90 containers unchanged. `/opt` free 55537 → 56434 MiB. A second dispatch, 41859429002 at `4ba5b0fd`, also succeeded. The first scheduled run is at 03:47 UTC on 2026-11-05. |
| P18 | Complete | 2026-11-04, no GrumPlates CD or maintenance in progress or queued. node-main: the CI stack of cancelled run 38004425567 (`books-ci-web`, `-redis`, `-postgres` `-38004425567-1`, running for 3 weeks) was stopped and removed without `-v`. Its network `books-ci-38004425567-1` and image `books:a5bd0287…-38004425567-1` (`sha256:0d00f9ca…`) were removed. The postgres container had no mounts, so no volume was left. The 30 exited containers labelled `localcluster.ci.repository=Grumlebob/BlazorAutoAppTemplate` were removed; unlabelled stopped containers were left alone (Q21). `/opt` free 33538 → 33579 MiB. node-demo: 33 images labelled with source `BlazorAutoAppTemplate` (books, books-react, `private-books[-react]-release-payload`) were removed without force, 0 refused. None was in use, current (`9bb714ee`), recovery (`9cb1b8c0`) or pinned. After P16 verification, 5 more untagged images with that label were removed (3e7b4955, 4d0b6ebc, 76bde5c9, 77b2d66e, f698826c). `/` used 100780 → 98734 MiB. demo.jacobgrum.com returned 200. Q11's orphan folder `/opt/books/release-payloads/12008462956-…` was deleted after the owner chose "Also delete orphan folder". |
| P19 | Complete | https://github.com/Grumlebob/GrumPlates/pull/437 (head `25c142e0`, PR CI 41798918357, full local gate PASS) merged `4a4de4bc`. Finding: the budget was compared against Docker's `Size`, which includes layers shared with images. `build_cache.py` now measures and trims the cache's own bytes. Verified by 41828469044: before 44.92 GB (25.23 GB own), after 40.85 GB (21.16 GB own). A second run left 21.26 GB own and trimmed nothing, because of a unit mismatch, so the budget was not yet met (Q22, P19b). |
| P19b | Complete | https://github.com/Grumlebob/GrumPlates/pull/440 (head `234730ad`, PR CI 41829436221, full local gate PASS) merged `64e1b9ff`, main CI 41838298870 success. Verified by LocalSingleNode maintenance primary 41849823525: 42.38 GB (22.37 GB own) → 39.89 GB (19.88 GB own, within the 20 GB budget). Additional 41850520928: already within budget, no trim. The rest of the cache belongs to CarbonCalculator's CI on the same daemon. |
| P21 | Complete | https://github.com/Grumlebob/GrumPlates/pull/438 (head `fd5440d0`, PR CI 41799053462, full local gate PASS) merged `51f2da7b`. The shared `mint_base` role (Deployment/Common/ansible) installs `/etc/systemd/journald.conf.d/50-grumplates-size.conf` (`SystemMaxUse=1G`, `SystemKeepFree=5G`) with a restart handler. Applied once on all six nodes with the same file. `journalctl --disk-usage`: node-main 2.4G → 974.2M, node-app1 1.7G → 1011.3M, node-app2 1.7G → 1019.7M, node-db 597.7M (unchanged), node-demo 1.8G → 1022.4M, portfolio-node 180.4M (unchanged). journald is active on all six. |
| P22 | Complete | https://github.com/Grumlebob/GrumPlates/pull/439 (head `77fe75b9`, PR CI 41801123274, full local gate PASS) merged `be6bbe7f`. Verified by LocalCluster maintenance 41829760651: payload-images stage removed 1 (e6434b67), refused 0. |
| P23 | Complete | https://github.com/Grumlebob/GrumPlates/pull/441 (head `ba453c50`, PR CI 41829731914, full local gate PASS) merged `9be28dfc`, main CI 41842245640 success. When the final maintenance ran (41849823525), node-demo had no untagged books image, so there was nothing to remove. The rule is covered by `test_old_untagged_image_is_removed_only_with_ownership_labels`. |
| P24 | Complete | https://github.com/Grumlebob/GrumPlates/pull/442 (head `406bdde1`, PR CI 41846611170, full local gate PASS) merged `4ba5b0fd`, main CI 41854110180 success. Verified by LocalCluster maintenance 41859429002: on node-app1 and node-app2 it removed digest-only books images adb8e44d, c3aeea24 and da5ff2a4 without force. The current and recovery digests (2c61991d, 55ab61a7) were kept. books images per app server 9 → 6, all under 7 days. |
| P25 | Complete | https://github.com/Grumlebob/GrumPlates/pull/443 (head `5d6d30c5`, PR CI 41869481811, full local gate PASS) merged `32dbde08`, main CI 41872753280 success. Verified downstream by Portfolio maintenance 41873704911 (D16). On node-demo no payload image was older than 24 h at closeout. |
| D11 Port P16 downstream | Complete | Portfolio https://github.com/Grumlebob/Portfolio/pull/211 (head `c52cb013`, CI 41800675869, full gate PASS) merged `2273109a`, main CI 41819513999. CarbonCalculator https://github.com/Grumlebob/CarbonCalculator/pull/456 (head `2540273a`, CI 41800672675, checked affected validation passed, Q23) merged `1fd38106`, main CI 41844730421. Verified: Portfolio maintenance 41846709693 removed 94 old app images (110 → 16); CarbonCalculator maintenance 41869061134 removed 17 (20 → 3). Both had 0 refusals. |
| D12 Automatic cleanup downstream | Complete | ImprovedDb https://github.com/Grumlebob/ImprovedDb/pull/438 (head `24645d58`, CI 41801101592; focused checks, see the D13 row for the post-merge gate) merged `b89a25cf`, main CI 41805150747: the `prune` job runs on its daily schedule in owned-only mode. Portfolio https://github.com/Grumlebob/Portfolio/pull/212 (head `a1be5547`, CI 41829489242, full gate PASS) merged `098be502`, main CI 41845059607: maintenance is scheduled daily and the BuildKit trim is ported (bytes). Verified by 41846709693: 40.64 GB (38.26 reclaimable) → 22.17 GB (19.79 GB own, budget 20); portfolio-node free 160349 → 178996 MiB. CarbonCalculator https://github.com/Grumlebob/CarbonCalculator/pull/457 (head `f3b46cd9`, CI green, checked affected validation passed, report 83204110877e499bb7691a9661a4ad18) merged `8f70915a`, main CI 41863871498. Verified by 41869061134: own bytes within its 60 GB budget. |
| D13 ImprovedDb node-main retention | Complete | Dry runs found that ImprovedDb's payload images had no retention at all (100 images, 26.8 GB unique; Q20). https://github.com/Grumlebob/ImprovedDb/pull/439 (head `123e752c`, CI 41802276477; focused checks: `test_prune_payload_images.py` 6 OK, `test-localcluster-maintenance.sh`, `audit-deployment.sh`) merged `a20cd191`, main CI 41806917577. ImprovedDb's local gate on main `a752d26b`, which contains #438 and #439, after merge: the template-shaped `repo-gate` passed 11 steps (diffcheck, restore, format, common, obs, audit, render, yamllint, shellcheck, playbooks, actionlint) and every applicable script test, including `test-localcluster-maintenance.sh`, `test-prune-docker-residue-low-disk.sh` and `test_prune_ci_residue.py`. Its profiles, react and image steps, the LocalSingleNode unit tests and `check-runner-capacity.sh` do not apply: they need template paths ImprovedDb does not have, or a CI-only `RUNNER_NAME`. ImprovedDb's binding check is its exact-SHA CI, green on `a20cd191` (41806917577). Applied by ImprovedDb maintenance 41810994967: all stages 0; payload images removed 93, refused 0; node-main `/opt` free 30195 → 56669 MiB. Archives, staged operations and unlabelled Testcontainers are kept (Q21; owner report). Dependabot #329 merged `a752d26b` after its rerun went green; main CI then failed in ImprovedDb's GHCR retention (Q26; owner report). |
| D14 Port P23 downstream | Complete | CarbonCalculator https://github.com/Grumlebob/CarbonCalculator/pull/459 (head `3e5b0673`, CI 41840016045, checked affected validation passed, report c4340cc7007848f0bbfab72d63fefe36) merged `b8993b5f`, main CI 41860029311. Portfolio https://github.com/Grumlebob/Portfolio/pull/213 (head `9ae6ea82`, CI 41840020574, full gate PASS) merged `879f9546`, main CI 41860006210. |
| D15 Portfolio payload retention | Complete | https://github.com/Grumlebob/Portfolio/pull/214 (head `f0efb573`, CI green, full gate PASS) merged `94ff64d6`, main CI 41863885929. Verified by maintenance 41866879296: all 60 payload images older than 24 h removed (68 → 8, all under 24 h). The run still failed on 22 false "No such image" refusals (Q27; fixed by D16). portfolio-node free 178783 → 180156 MiB. |
| D16 Port P25 to Portfolio | Complete | https://github.com/Grumlebob/Portfolio/pull/215 (head `cef5c81e`, CI 41869487510, full gate PASS) merged `2f69063b`, main CI 41872775947. Verified by maintenance 41873704911: success; payload images removed 1, refusals 0; BuildKit 20.99 → 20.65 GB (19.70 GB own). |
| P20 | Complete | See the closeout below. All three targets were redeployed at `32dbde08` with `run_migrations=true`: primary CD 41876633649, additional 41879485608, LocalCluster 41881861659 (backup `/opt/books/backups/predeploy-20261104T180327Z.sql.gz` on node-db). Release status: primary 41883603874, additional 41883621146, LocalCluster 41883640567, all success. Capacity: node-main `/opt` free 55825 MiB against the 20480 MiB reserve, a margin of 35345 MiB (P17's check in run 41859429002: free_mb=56291). |

### P20 closeout (2026-11-04)

Before is the section 4.12 measurement (after P14); after is 2026-11-04 evening: node-main, node-app1/2 and node-db before the final deploy; node-demo and portfolio-node after D16's verification.

| Node | Used (before → after) | Free | Images (reclaimable) after | Build cache after | Journal after |
| --- | --- | --- | --- | --- | --- |
| node-main | 90% → 76% | 23 GB → 56 GB | 21.0 GB (14.5 GB) | 26.5 GB (18.9 GB) | 982 MB |
| node-demo | 45% → 40% | 124 GB → 134 GB | 15.8 GB (13.6 GB); 23 books and 23 books-react images, all under the 48 h age limit after the day's releases | 40.7 GB (24.4 GB), shared with CarbonCalculator; books own within 20 GB | 1019 MB |
| portfolio-node | 30% → 21% | 157 GB → 177 GB | 3.4 GB (2.6 GB) | 20.7 GB (19.7 GB own) | 180 MB |
| node-app1 | 16% → 15% | 188 GB → 189 GB | 5.6 GB (2.8 GB) | 0 | 1011 MB |
| node-app2 | 16% → 15% | 188 GB → 189 GB | 5.6 GB (2.8 GB) | 0 | 1020 MB |
| node-db | 10% → 10% | 201 GB → 201 GB | 2.3 GB (1.0 GB) | 0 | 598 MB |

Owner report: what this plan does not change. Sizes are from the after measurement.

| Item | Size | Safe owner action | Risk |
| --- | --- | --- | --- |
| ImprovedDb `LocalData` on node-main | 63 GB | None; it is application data. | — |
| ImprovedDb `maintenance-volume-archives` on node-main | 16 GB (6 directories from 2026-09-23 plus `retired-testcontainers-20260923T112155Z`) | Decide whether the legacy PostgreSQL recovery archives are still needed. If not, change ImprovedDb's documented policy ("Protected; no automatic expiration") and expire them through its code. | Losing old recovery points. |
| ImprovedDb `staged-operations` on node-main | 2.9 GB (directories from 2026-10-06/07) | Confirm those operations finished, then let ImprovedDb's release-residue scope remove them. | Low, if the operations are finished. |
| 76 unlabelled stopped Testcontainers on node-main (58 postgres, 18 redis; 9 sessions on 2026-10-08) and their 58 anonymous volumes | about 2.4 GB | They predate ImprovedDb's test labels (675bf5e8). If they are ImprovedDb's, remove them with `docker rm` (no `-v`), then review those volumes. | Low; three repositories ran Testcontainers there. |
| node-main shared BuildKit cache | 26.5 GB (18.9 GB reclaimable) | The template prunes it only with `--allow-shared-builder-prune`. Decide whether builds on node-main may lose cache. | Slower next builds for every app. |
| ImprovedDb app images on node-main | 64 images, about 14.5 GB reclaimable | ImprovedDb's daily maintenance (D12) applies its 7-day retention; check after a week. | — |
| `ghcr.io/grumlebob/ship` images on node-app1 and node-app2 | 14 each, 5 months old, unlabelled, unused; about 2.7 GB reclaimable per node | Identify the owner. If unused, remove them by ID without force. | Low. |
| NuGet cache `/home/deploy/.nuget` on node-main | 7.1 GB, shared by two runners | `dotnet nuget locals all --clear` as deploy, while no job runs. | Slower next restore. |
| CarbonCalculator on node-demo | 3 app images after D11/D14; its cache is within its own 60 GB budget | None. | — |
| Portfolio on portfolio-node | 16 app images, payload images under 24 h, cache 22 GB (19.8 GB own) | None; daily maintenance keeps it bounded. | — |
| journald | Capped at 1 GB on every node (P21) | None. | — |
| ImprovedDb main CI | `a752d26b` failed in GHCR retention (Q26) | Resume reservation `run-41814090682-1` with ImprovedDb's coordinator, then rerun main CI. | None on hosts. |
| CarbonCalculator runner flake | Q24 | Look at the Playwright trace capture on `node-demo-carboncalculator`. | — |

Deleted task branches (full SHAs), each merged at that head:
- GrumPlates `cleanup/lean-p16-image-tags-20261104` `0ebcfba43feb23bd94c8876cb535eae926e5845b` (PR #435)
- GrumPlates `cleanup/lean-p17-daily-cluster-cleanup-20261104` `21f14fb41df26973e7a6e867359b34e3c13fd030` (PR #436)
- GrumPlates `cleanup/lean-p19-cache-measure-20261104` `25c142e06ce463b579ef9dc886054c131f7870e7` (PR #437)
- GrumPlates `cleanup/lean-p19b-cache-units-20261104` `234730ad6c60c74997ef93e65176dd206fba7ff2` (PR #440)
- GrumPlates `cleanup/lean-p21-journald-cap-20261104` `fd5440d0b757355bc5fa95b2f6d1377de7540c54` (PR #438)
- GrumPlates `cleanup/lean-p22-cluster-payload-images-20261104` `77fe75b913810fa07e138f85cefcb9b8f3ae919e` (PR #439)
- GrumPlates `cleanup/lean-p23-untagged-own-images-20261104` `ba453c5077d3aaf58413002f6582657c01ae1a6a` (PR #441)
- GrumPlates `cleanup/lean-p24-digest-only-app-images-20261104` `406bdde1704188594b18d459b85931040f4f81d2` (PR #442)
- CarbonCalculator `cleanup/lean-d11-image-tags-20261104` `2540273a3f725c330e1ab4ff49f5e5493bd7cfae` (PR #456)
- CarbonCalculator `cleanup/lean-d12-daily-maintenance-20261104` `f3b46cd9f828c351eb860ac927bef0eefb484d97` (PR #457)
- CarbonCalculator `cleanup/lean-d14-untagged-own-images-20261104` `3e5b0673cc06c1f34f591f9ffbe194f62b5d3566` (PR #459)
- Portfolio `cleanup/lean-d11-image-tags-20261104` `c52cb013bd76e5918c614873ae348cd219cbee0c` (PR #211)
- Portfolio `cleanup/lean-d12-daily-maintenance-20261104` `a1be5547c492ba27da9869724f03a8d77216a02a` (PR #212)
- Portfolio `cleanup/lean-d14-untagged-own-images-20261104` `9ae6ea82912c7130173730c5f6f401e7c02692b2` (PR #213)
- Portfolio `cleanup/lean-d15-payload-images-20261104` `f0efb573f12e7242c84257ca3af849fa4ed05352` (PR #214)
- ImprovedDb `cleanup/lean-d12-daily-host-cleanup-20261104` `24645d583de917bb1a8afd55c760c59042ee9df0` (PR #438)
- ImprovedDb `cleanup/lean-d13-payload-images-20261104` `123e752c50db5238b13d5663fdf5ee250246ca2f` (PR #439)
- GrumPlates `cleanup/lean-p25-payload-digest-tags-20261104` `5d6d30c57c8582d757fd40464d36548b318a078f` (PR #443)
- Portfolio `cleanup/lean-p25-payload-digest-tags-20261104` `cef5c81e059527e835b97273a6f540794c777cfa` (PR #215)
- ImprovedDb `lean-gate/improveddb-main-20261104` `a752d26b9380f857bf45b02e69efca5bfe244bdc` (local gate branch, contained in main)

P15 step 5 residue search (2026-11-04, on origin/main): every hit has a keep reason.
- `find-successful-ci-run.py` publisher fallback, `test_ci_provenance` legacy publisher test: kept (P06; historical CI runs still use the `build-test-push` publisher name).
- `prune-actions-artifacts.py` `--keep 0`, `test_prune_actions_artifacts`, audit `--keep 0` rules, the prune-script comments: kept (P06; "legacy" names the copies the pruner removes).
- `test_release_status.py` `legacy-123-`: kept (P06; negative test).
- `test_single_node.py` `payload("legacy", …)`: kept (P06; host cleanup still removes unprefixed payload images).
- `test_app_instance.py`, `test_settings_fixture.py` legacy-settings tests: kept (P06; settings without account keys default to off, current behaviour).
- `test_workflow_policy.py` legacy download reader: kept (P06).
- `patch-redocly-minimatch.mjs` and its test: kept (P06; upstream import form).
- `sonar_report.py` `LEGACY_SEVERITY`, `test_sonar_report.py`, `Test-SonarQualityModule.ps1`: kept (P06; Sonar's own legacy issue type).
- `10k-most-common.txt`, `account-password-field.test.tsx` "legacy": kept (P06; data and test input).
- Both `validate-rendered-templates.sh` `Accounts__Enabled`: kept (P03; guards that the removed switch is never rendered).
- `GrumPlates.React/package.json` `>=26.12.1 <27`: kept (P08 step 8; the engine range itself).

Deleted task branches (full SHAs):
- `cleanup/lean-p06-20261103` `0943e3c6770dde6fc15339b904dfda14749e5114`
- `cleanup/lean-p07-20261103` `3528ec9da8fe6c7c30b36d59a6a80bd519ae8e94`
- `cleanup/lean-p08-20261103` `cf8ec9ad8e28c223d58d7500191d810cdf583d87`
- `cleanup/lean-p09-20261103` `0035825cf2ffc26af4d9ed2f8ad342ee740aa051`
- `cleanup/lean-p10-20261103` `a6756bffbc1b083415cab7f8f822136f13a515c9`
- `cleanup/lean-p11-20261103` `c83831181ba5345d70ba116c00aa103189870d63`
- `cleanup/lean-p12-20261103` `a50f5e0a65c469ad072260df32a882eb27a03862`
- `cleanup/lean-p14-react-acceptance-20261104` `141c912a930d4a5377b9de1f1632cf91cdb53c48`
- `cleanup/lean-p15-closeout-20261104`: deleted when this closeout PR merges; its SHA is in the PR.

Packet entry template:

```text
Packet / status:
Input origin/main SHA and branch:
What was removed / kept (with reasons):
Focused checks and full local gate:
PR URL, exact head SHA, build-test-push run:
Merge SHA and exact-main CI run:
Deleted branch SHAs:
```
