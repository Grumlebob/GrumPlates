Complete Plans/LeanTemplateCleanup.md: the GrumPlates template packets P00-P15 and the downstream packets D01-D10. The plan is the runbook; read it fully. Each repository's AGENTS.md and docs/Test.md win on conflict. Always pass -R Grumlebob/<Repo> to gh.

Outcome:
- GrumPlates is lean: no legacy-state code; accounts always on with one hardened seed; GHCR-only releases; one squashed migration; no runner routing; no Markdown-reading audit rules; one Node version source; tidy config, plans, branches and PRs.
- Its three live targets are deployed and verified. Blazor and React both stay fully supported.
- CarbonCalculator, ImprovedDb and Portfolio get only the small fixes in section 7, and their failing Dependabot PRs are unblocked. JobHunt gets branch hygiene only.

Decisions OD-1 to OD-18 are final. For a new choice, take the leaner safe option, record it as Q5+ in the plan, and continue. Ask only if every option risks live data, security or a supported feature.

Follow the section 5 order. One packet per PR, from fresh origin/main in a worktree; never use a primary checkout with others' work (ImprovedDb has some).
- Code PRs: focused checks and the repository's full gate first (repo-gate in the background). Merge only when build-test-push is green on the exact head; then confirm green CI on the merge commit.
- Docs-only PRs: [skip ci] and git diff --check.
- Stage explicit paths. Never commit secrets or .env. Never force-push main. Never weaken a test, gate or audit, and never pin test counts.
- Evidence goes in PR descriptions; the next GrumPlates PR updates the ledger.

Authorisation (granted by starting this goal), limited to the plan's steps:
- GitHub: delete coord/node-inventory-handoff, the CI_RUNNER_ROUTING_ENABLED variable, and downstream branches D10 proves integrated; comment on and merge Dependabot PRs once green; close Portfolio #197.
- Hosts: P01-B LocalCluster catch-up deploy; P14 backups, the guarded history rewrite on the three books databases, and CD for LocalSingleNode (primary, additional) and LocalCluster at one green main SHA with run_migrations=true, then Release status; D09 read-only pin listing.
- Nothing else on hosts:
  - no Cloud or downstream deploys;
  - no prune, volume deletion or database drops;
  - no locks you did not take;
  - never touch CarbonCalculator, ImprovedDb or Portfolio host resources.
- Never deploy with run_migrations=true between P07's merge and P14 step 3. If a backup, schema diff or guard fails, stop that target and report.

Done when:
- every ledger row, D01-D10 included, is Complete with evidence;
- GrumPlates main CI is green and each target passed acceptance and release status on the final SHA;
- the plan's open-PR and branch checks pass in all five repositories;
- the P15 residue grep has a keep-reason for every hit;
- the plan is marked complete on main.

Final report: changes per repository, PR numbers, run IDs, backup paths, kept branches, and the downstream changes that first go live on the owner's next deploy.
