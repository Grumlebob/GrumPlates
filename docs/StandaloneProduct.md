# Turn a fork into a standalone product

Use this when a fork becomes a product of its own instead of a configured copy of the template. The [fork guide](HowToForkThisRepo.md) still owns identity, runner variables and release settings; this page adds the order of work and the template material to remove.

## Create the repository

1. Create the empty product repository. Disable Actions until a runner for it exists (`gh api -X PUT repos/<owner>/<repo>/actions/permissions -F enabled=false`). Every job runs on self-hosted labels and otherwise queues indefinitely.
2. While the repository is still empty, apply the settings the workflow expects. Pull-request auto-merge is used by the Dependabot workflow and agents, and merged branches are deleted automatically. Every workflow declares its own token permissions, so the default token stays read-only:

   ```bash
   gh api -X PATCH repos/<owner>/<repo> -F allow_auto_merge=true -F delete_branch_on_merge=true \
     -F allow_update_branch=true -F allow_squash_merge=true -F allow_merge_commit=true
   gh api -X PUT repos/<owner>/<repo>/actions/permissions/workflow \
     -f default_workflow_permissions=read -F can_approve_pull_request_reviews=false
   ```

   Keep merge commits enabled: the Dependabot workflow merges with `--merge`. Add branch protection only after the first push (an empty repository has no `main`). On a public repository, requiring `build-test-push` also blocks `[skip ci]` documentation PRs, because their check never runs. Decide which you want before enabling it.
3. Clone the template with its history and point the remotes at the product:

   ```bash
   git clone https://github.com/<template-owner>/<template-repo>.git <Product>
   cd <Product>
   git remote rename origin upstream
   git remote add origin https://github.com/<owner>/<repo>.git
   git push -u origin main
   ```
4. Run `gh repo set-default <owner>/<repo>` in the new checkout immediately. `gh` prefers a remote named `upstream`, so without it `gh pr merge`, `gh run list` and scripts that call `gh repo view` act on the template.
5. Commit one seed change for identity, frontend profile and release settings as described in the fork guide. Keep the internal project names until the rename step. Put the product's final `app_image` in this seed, before main CI first publishes. A GHCR package created by the repository's own workflow grants that repository access automatically. If the package already exists (pushed by hand, by another repository or under an earlier name), give the repository the Write role under the package's **Settings → Manage Actions access**. GitHub offers that setting only in the web UI.
6. Expect Dependabot to open update PRs right after the first push, against the template's paths. Leave them until the rename and removals land. Then close the ones whose directories no longer exist (`gh pr close <n>`); Dependabot recreates the rest with the new paths.

## Runners and plan limits

- On a personal GitHub account, a self-hosted runner belongs to one repository. A machine that already runs another repository's runner needs a second registration for this repository. The LocalSingleNode installer keeps each app's runner in its own folder. Set `CI_RUNNER_LABEL`, `CI_RUNNER_HOST` and the target runner label before enabling Actions.
- On GitHub Free, private repositories have no branch protection or rulesets. `build-test-push` is then required by the working rules in `AGENTS.md`, not by GitHub. Private images also count toward the account's GitHub Packages storage.

## Order of simplification

Every pull request costs a full [local gate](Test.md#local-gate) (about 15 minutes) and an exact-head `build-test-push`, so batch the steps. Use a few large PRs, not one per step. RitzauDemo removed React and the unused targets in one PR (310 files), then renamed and reorganized in the next (371 files), both on the same day. Keep the rename out of the deletion PR so its diff stays mechanical. When PRs are stacked, retarget the dependent PR to `main` before merging the lower one.

1. **Seed** (above), with green CI.
2. **One frontend:** follow [React-only clone](Frontend/ReactOnlyClone.md) when Blazor is not needed.
3. **One deployment target:** follow [single-node-only clone](../Deployment/LocalSingleNode/SingleNodeOnlyClone.md) or the equivalent for the kept target.
4. **Rename** after the deletions, so deleted code is never renamed first:

   | Template name | Product name |
   | --- | --- |
   | `GrumPlates.sln`, `GrumPlates`, `.Core` and `.Test` projects, folders and root namespaces | `<Product>.sln`, `<Product>`, `<Product>.Core`, `<Product>.Test` |
   | `GrumPlates.React`, npm package `grumplates-react` | `<Product>.React`, `<product>-react` |
   | Image labels `com.grumplates.*` | A product label namespace. Change the Dockerfile, manifest writer and validator, release verifiers, smoke test, tests and deployment audit together |
   | Alloy container filter `grumplates` in `Scripts/LocalObservability/alloy/config.alloy` | The product's Compose project name |

   Keep shared host names unchanged: the deployment lock `/tmp/localcluster-deploy.lockdir` and the CI resource labels are shared with other apps on the same machine. Run `api:check` afterwards; the generated contract should not change.
5. **Remove template material** (next section).
6. **Replace the sample feature** with the product's first use case. Use the [feature guide](HowToAddANewFeature.md), and remove the sample's data, tests, docs and observability with it.

## Remove template material

| Material | Action |
| --- | --- |
| `Plans/` template plans and `Plans/Support/` | Remove. Start the product's own plans and keep `Plans/README.md`. Git history and the template keep the old plans |
| Fork guide, this page, [React-only clone](Frontend/ReactOnlyClone.md), `SingleNodeOnlyClone.md` | Remove after use. The deployment audit reads the fork guide, so update the audit in the same code PR |
| `README.md`, `docs/Requirements.md` | Describe the product and its requirements. A one-line provenance link to the template is enough |
| Favicon | The React app ships none, so browsers log a 404 for `/favicon.ico`. Add the product's icon under the React app's `public/` folder |
| `AGENTS.md` | Keep the working rules; drop rules for removed parts, such as the Razor stylesheet rebuild |
| `.claude/skills/`, `.codex/`, `.qlty/`, `.run/` | Keep what the team uses and update names and paths |
| `Scripts/Quality` (including `Scripts/Quality/SonarQube`) | Keep deliberately or remove with their docs |
| `.github/dependabot.yml` | Keep; update directories after renames |

Finish with a search and classify every match:

```bash
git grep -nIiE "GrumPlates|Books" -- ':!Plans'
```

Keep only provenance links, intentional shared names and recorded follow-ups.

## Public site behind Cloudflare

The React host sends a strict Content Security Policy. On HTML responses it adds a fresh `'nonce-…'` to `script-src` ([frontend profiles](FrontendProfiles.md)), so Cloudflare JavaScript detections can inject their script without `'unsafe-inline'` and the browser console stays clean. While detections run, Lighthouse best practices scores about 81, because Cloudflare's script calls a deprecated browser API; Portfolio accepted that to keep the bot signal. To trade the signal for the score, exclude the hostname from JavaScript detections in the zone. The live browser tests already ignore Cloudflare's own edge cookies.

## Sharing a node with another product

Several apps can share a LocalSingleNode machine. Each app owns its Compose project, runtime root, Caddy site, ports and subnet, and shares Docker, Caddy and the deployment lock ([deployment overview](../Deployment/README.md)). [Application instances](../Deployment/LocalSingleNode/ApplicationInstances.md) covers only a second profile of the *same* repository.

A different repository joins a node through the normal [AgentSetup](../Deployment/LocalSingleNode/AgentSetup.md), run from that repository's own checkout on the node:

- Use the node's existing name as `--node`; bootstrap renames the host when the name differs.
- The existing runner belongs to the other repository and cannot run this one's jobs. The root bootstrap step registers an app-specific runner for the checkout's `origin` repository.
- Bootstrap records each app separately in `/etc/localsinglenode/bootstrap.json`. The shared root Caddyfile imports `/etc/caddy/sites/*.caddy` and each app writes only its own site file, so the other app's records and site stay in place.
- Choose an app name, ports, roots and Docker subnet that differ from every existing app. Setup's collision check (`Deployment/LocalSingleNode/Scripts/check-port-collisions.sh`) rejects overlaps before anything starts.
- The operator runs the one root command that setup prints. Never change the other app's resources, tunnel or DNS.

This route follows the bootstrap design but had not been run on a real node when it was written; record the first run's outcome in the product's plan.
