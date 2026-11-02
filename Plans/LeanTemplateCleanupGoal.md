Complete Plans/LeanTemplateCleanup.md in GrumPlates (GitHub Grumlebob/GrumPlates; always pass -R Grumlebob/GrumPlates). The plan is the runbook: read it fully, then AGENTS.md and docs/Test.md, which win on conflict.

Outcome: a lean template with no legacy-state code. Accounts are always on. There is one hardened account seed. The release path is GHCR-only. One squashed migration. No runner routing. No Markdown-reading audit rules. One Node version source. Tidy configuration and plans. Clean branches and PRs. All three live targets deployed and verified. Blazor and React both stay fully supported frontend choices.

Decisions OD-1 to OD-15 are final; do not ask them again. If a new choice appears, take the leaner safe option, record it as Q5+ in the plan, and continue. Ask the owner only if every option risks live data, security or a supported feature.

Execute in the plan's section 5 order, one packet per PR (P03 has five), each branched from fresh origin/main in a worktree:
- Code/config PRs: focused checks, then the full local gate (repo-gate from Git Bash, in the background). Merge only when build-test-push is green on the exact PR head; then wait for green CI on the exact main merge commit.
- Docs-only PRs: [skip ci] in the commit and squash title, plus git diff --check (and the deployment audit until P11 merges).
- Stage explicit paths. Never commit secrets or .env. Never force-push main.
- Evidence goes in the PR description. The next PR updates the ledger rows.
- If a check fails, fix the cause. Never weaken a test, gate or audit to pass. Never pin test counts.

Authorisation (the owner grants it by starting this goal), limited to exactly the plan's steps:
- Delete branch coord/node-inventory-handoff and repository variable CI_RUNNER_ROUTING_ENABLED.
- Comment on and merge the Dependabot PRs #405, #406, #407 when green.
- P01-B: dispatch CD - Deploy LocalCluster at current green main with run_migrations=true.
- P14: LocalSingleNode maintenance backups (primary, additional), the LocalCluster backup-db.sh, the guarded __EFMigrationsHistory rewrite on the three books databases, CD - Deploy LocalSingleNode (primary, additional) and CD - Deploy LocalCluster at one green main SHA with run_migrations=true, then Release status for each.
Nothing else on hosts. Never dispatch Cloud. Never prune Docker, delete volumes or drop databases. Never release a lock you did not take. Never touch CarbonCalculator, ImprovedDb or Portfolio resources. Between P07's merge and P14 step 3, never deploy with run_migrations=true. If a backup, schema diff or history guard fails, stop that target and report.

Done when:
- every packet's ledger row is Complete with evidence;
- origin/main CI is green;
- the open PR list is empty (or only holds new Dependabot PRs, handled);
- the only branches are main and the improveddb/* remote branches;
- the P15 residue grep has a recorded keep-reason for every hit;
- every live target passed acceptance and release status on the final SHA;
- the plan is marked complete on main.

Final report: what changed, deploy run IDs, backup paths, and the downstream port list.
