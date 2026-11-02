# Lean template cleanup

Status: **not started**. Research and inventory were completed on 2026-11-02 against `origin/main` `d28a6c7b3afb0a406a217a5508e103de6227e2f8`. The owner answered every open question on 2026-11-02 (OD-6 to OD-9). A review on the same day added recommendations OD-10 to OD-15 and verified the live state again. No question blocks execution. The live-target work (P01-B and P14) needs operator authorisation. [LeanTemplateCleanupGoal.md](LeanTemplateCleanupGoal.md) grants it when the owner starts that goal.

This is an execution runbook for an agent that has no memory of the conversation that produced it. Do one packet at a time, in the order in section 5. Record evidence as section 3 describes. If a packet's checks fail, stop that packet and fix it or report. Do not skip ahead.

## 1. Goal

Make GrumPlates a lean template:

1. **No legacy states.** Remove code, settings, workflows, tests and docs that exist only to migrate, import, pin, lock or tolerate state from earlier versions of this repository or its deployments. A fresh clone never has such state.
2. **One account model.** Accounts are always on and always use the hardened account code. Remove the `Accounts:Enabled` switch, the original-scaffold Blazor Identity behaviour it kept alive, and the separate `LocalAccounts` demo seed.
3. **Fewer brittle parts.** Remove settings and checks that duplicate each other, pinned values that must be edited in many places, and checks that read prose documentation.
4. **Total repository hygiene.** Remove stale branches, resolve open PRs, delete finished plans, and remove local build leftovers.

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

Earlier decisions that still hold: Identity (with MFA, passkeys, external login) stays; Cloud, LocalCluster and LocalSingleNode stay as supported template targets; do not add "absence" tests that prove a removed thing is gone (an earlier owner rule, recorded in `Plans/TemplateCleanupAndFeatureRetirement.md` section 10, which packet P12 deletes).

## 3. Rules for the executor

Read [AGENTS.md](../AGENTS.md) and [docs/Test.md](../docs/Test.md) completely before starting. They override anything here that conflicts. The most important rules:

- **One PR per packet** (P03 has five sub-PRs). Branch from fresh `origin/main`. Name branches `cleanup/lean-<packet>-<yyyymmdd>`, for example `cleanup/lean-p02-20261103`.
- **Evidence and ledger.** Put the packet's evidence (template at the end of section 12) in its PR description before merge. Do not push a separate PR just to update the ledger. Instead, the **next** packet's PR also changes that packet's ledger row to `Complete`, with its PR URL, merge SHA and exact-main CI run. P15 records the last ones. GitHub-only and host-only packets (P00, P01-A, P01-B, P13, P14) record their evidence in the next PR the same way.
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
| P15 | Closeout | everything | GitHub/local |

Execute in this exact order: P00, P01-A, P01-B, P02, P03-A … P03-E, P04, P05, P06, P07, P08, P09, P10, P11, P12, P13, P14, P15. Merge each PR before branching the next from fresh `main`. P04 and P05 both edit `ci.yml` and the audit; P05 rebases on P04's removals.

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
8. Give the owner the downstream list from section 7 and the final evidence summary.

## 7. Downstream repositories

CarbonCalculator and Portfolio were created from this template and are ported by hand. This plan does **not** change them. After P15, give the owner a short list of the template PRs that downstream repos may want to port: accounts always on, legacy DP import removal, legacy release path removal, Node single-source, configuration tidy. Do not open downstream PRs without the owner asking.

## 8. Out of scope

- Quality tools (SonarQube, Qlty, Lighthouse) stay. They are optional and not merge gates.
- Package upgrades beyond the Dependabot PRs, framework changes and Book feature redesigns.
- The owner's live infrastructure files that the template uses to deploy its own demo (`Deployment/LocalCluster/inventory/prod/vault.yml`, `hosts.yml`, the tracked `group_vars`). Forks replace them per `docs/HowToForkThisRepo.md`.
- `Plans/CloudflaredConnectorFollowUps.md` work (connector pin bump etc.). It is a separate open plan.
- Q-002 (Caddy MD5 check), Q-003, Q-004 in `Plans/CodeQualityImprovements.md`.

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
| Seeded passwords leak | They are generated into the ignored `.env`, never printed, never logged, and never stored in tracked files. Deployments do not get a User seed. |
| Developers and CI without a key-encryption certificate | Docker local setup creates `docker/https/aspnetapp.pfx`; Windows Development uses DPAPI; Linux/macOS Development falls back to unencrypted keys with a warning (OD-10); `ci-docker-smoke.sh` generates a certificate (P03-B step 10); deployments mount one from the vault. |
| Existing developer `.env` files lack seeded accounts | `setup-local.ps1` fills unconfigured seeds in place (OD-12). |

## 10. Manual blockers (owner)

None block execution. Starting [LeanTemplateCleanupGoal.md](LeanTemplateCleanupGoal.md) is the authorisation for P01-B and P14 (backups, history rewrites, CD dispatches, release status), for deleting the stale branch and the unused repository variable. Remove that paragraph from the goal before starting it if you want to do those steps yourself.

Optional, owner only: `agent.md` in the repo root is your private, ignored notes file with outdated SSH details and Codex instructions. Agents leave it alone.

## 11. Questions answered by the owner (2026-11-02)

| ID | Question | Answer |
| --- | --- | --- |
| Q1 | With accounts always on, registration needs SMTP. How do local development and Blazor E2E get users? | Seeded accounts, no local mail server (OD-6; P03-C, P03-E). |
| Q2 | Squash the four EF migrations? | Yes (OD-7; P07). The plan keeps live data by rewriting migration history (P14 step 3) instead of recreating databases. |
| Q3 | Remove the never-enabled CI runner profile routing? | Yes (OD-8; P05). |
| Q4 | Remove deployment-audit rules that read Markdown? | Yes (OD-9; P11). |

If execution uncovers a new decision: when one option is clearly leaner and safe, take it, record it here as Q5 onwards with the reason, and continue. Ask the owner only when every option risks live data, security or a supported feature (both frontends, the three deployment targets).

## 12. Ledger

| Item | Status | Evidence |
| --- | --- | --- |
| Research and inventory | Complete | 2026-11-02 at `origin/main` `d28a6c7b3afb0a406a217a5508e103de6227e2f8`; sections 4.1–4.11. |
| Owner answers Q1–Q4 | Complete | 2026-11-02: seeded accounts; squash; remove routing; remove Markdown audit rules (OD-6 to OD-9). |
| Plan review | Complete | 2026-11-02: OD-10 to OD-15; live databases, pins, images and deploy inputs verified read-only (section 4.2). |
| P00 | Pending | |
| P01-A | Pending | |
| P01-B | Pending (authorised by the goal) | |
| P02 | Pending | |
| P03-A | Pending | |
| P03-B | Pending | |
| P03-C | Pending | |
| P03-D | Pending | |
| P03-E | Pending | |
| P04 | Pending | |
| P05 | Pending | |
| P06 | Pending | |
| P07 | Pending | |
| P08 | Pending | |
| P09 | Pending | |
| P10 | Pending | |
| P11 | Pending | |
| P12 | Pending | |
| P13 | Pending | |
| P14 | Pending (authorised by the goal) | |
| P15 | Pending | |

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
