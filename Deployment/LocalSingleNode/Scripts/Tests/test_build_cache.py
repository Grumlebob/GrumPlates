from pathlib import Path
import sys
from unittest import TestCase

import yaml

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "lib"))

import build_cache  # noqa: E402

DF_OUTPUT = "\n".join([
    '{"Active":"5","Reclaimable":"40.14GB (83%)","Size":"47.93GB","TotalCount":"417","Type":"Images"}',
    '{"Active":"9","Reclaimable":"0B (0%)","Size":"110.6kB","TotalCount":"9","Type":"Containers"}',
    '{"Active":"9","Reclaimable":"22.11GB (99%)","Size":"22.31GB","TotalCount":"459","Type":"Local Volumes"}',
    '{"Active":"0","Reclaimable":"48.85GB","Size":"82GB","TotalCount":"843","Type":"Build Cache"}',
])


class FakeDocker:
    def __init__(self, df_outputs, prune_help="  --keep-storage bytes   Amount of disk space to keep for cache"):
        self.df_outputs = list(df_outputs)
        self.prune_help = prune_help
        self.calls = []

    def __call__(self, *args, timeout=60):
        self.calls.append(args)
        if args[:3] == ("docker", "system", "df"):
            return self.df_outputs.pop(0)
        if args == ("docker", "builder", "prune", "--help"):
            return self.prune_help
        if args[:3] == ("docker", "builder", "prune"):
            return "Total reclaimed space: 62GB"
        raise AssertionError(f"unexpected command {args}")

    def prunes(self):
        return [call for call in self.calls if call[:3] == ("docker", "builder", "prune") and "--help" not in call]


class BuildCacheTests(TestCase):
    def test_sizes_parse_as_docker_prints_them(self):
        self.assertEqual(82_000_000_000, build_cache.parse_size("82GB"))
        self.assertEqual(48_850_000_000, build_cache.parse_size("48.85GB"))
        self.assertEqual(110_600, build_cache.parse_size("110.6kB"))
        self.assertEqual(0, build_cache.parse_size("0B"))
        with self.assertRaises(ValueError):
            build_cache.parse_size("lots")

    def test_only_the_build_cache_row_is_read(self):
        self.assertEqual((82_000_000_000, 48_850_000_000), build_cache.build_cache_bytes(DF_OUTPUT))
        self.assertEqual((0, 0), build_cache.build_cache_bytes(DF_OUTPUT.splitlines()[0]))

    def test_the_cap_flag_is_preferred_on_newer_docker(self):
        self.assertEqual("--keep-storage", build_cache.keep_flag("  --keep-storage bytes"))
        self.assertEqual("--reserved-space", build_cache.keep_flag("  --reserved-space bytes\n  --keep-storage bytes"))
        docker_29 = ("      --max-used-space bytes   Maximum amount of disk space allowed to\n"
                     "      --min-free-space bytes   Target amount of free disk space after pruning\n"
                     "      --reserved-space bytes   Amount of disk space always allowed to\n")
        self.assertEqual("--max-used-space", build_cache.keep_flag(docker_29))

    def test_budget_is_passed_in_the_decimal_bytes_that_df_reports(self):
        # Docker reads "20GB" as 20 GiB; on node-demo that left 21.26 GB (df units) after a 20 GB trim.
        command = build_cache.prune_command(20, "--max-used-space")
        self.assertEqual(["docker", "builder", "prune", "--force", "--max-used-space", "20000000000"], command)
        self.assertEqual(20 * 1000 ** 3, build_cache.parse_size("20GB"))

    def test_trim_prunes_only_build_cache_down_to_the_budget(self):
        after = DF_OUTPUT.replace('"Reclaimable":"48.85GB","Size":"82GB"', '"Reclaimable":"19GB","Size":"52GB"')
        docker = FakeDocker([DF_OUTPUT, after])
        self.assertEqual(0, build_cache.main(["--keep-gb", "20"], runner=docker))
        self.assertEqual([("docker", "builder", "prune", "--force", "--keep-storage", "20000000000")], docker.prunes())
        for call in docker.calls:
            self.assertNotIn("system prune", " ".join(call))
            self.assertNotIn("volume", call)
            self.assertNotIn("image", call)

    def test_cache_within_budget_is_left_alone(self):
        small = DF_OUTPUT.replace('"Reclaimable":"48.85GB","Size":"82GB"', '"Reclaimable":"12GB","Size":"12GB"')
        docker = FakeDocker([small])
        self.assertEqual(0, build_cache.main(["--keep-gb", "20"], runner=docker))
        self.assertEqual([], docker.prunes())

    def test_bytes_shared_with_images_do_not_count_against_the_budget(self):
        # node-demo, 2026-11-04: 43.37 GB in total, 21.79 GB the cache's own (buildx du Private).
        shared = DF_OUTPUT.replace('"Reclaimable":"48.85GB","Size":"82GB"', '"Reclaimable":"19.5GB","Size":"43.37GB"')
        docker = FakeDocker([shared])
        self.assertEqual(0, build_cache.main(["--keep-gb", "20"], runner=docker))
        self.assertEqual([], docker.prunes())
        over = DF_OUTPUT.replace('"Reclaimable":"48.85GB","Size":"82GB"', '"Reclaimable":"21.79GB","Size":"43.37GB"')
        after = DF_OUTPUT.replace('"Reclaimable":"48.85GB","Size":"82GB"', '"Reclaimable":"20GB","Size":"41.58GB"')
        docker = FakeDocker([over, after])
        self.assertEqual(0, build_cache.main(["--keep-gb", "20"], runner=docker))
        self.assertEqual(1, len(docker.prunes()))

    def test_dry_run_never_prunes(self):
        docker = FakeDocker([DF_OUTPUT])
        self.assertEqual(0, build_cache.main(["--dry-run"], runner=docker))
        self.assertEqual([], docker.prunes())

    def test_budget_outside_the_safe_range_is_rejected(self):
        for value in ("0", "4", "501"):
            with self.assertRaises(ValueError):
                build_cache.main(["--keep-gb", value], runner=FakeDocker([DF_OUTPUT]))

    def test_scheduled_retention_job_trims_the_cache_under_the_deployment_lock(self):
        workflow = yaml.safe_load((ROOT / ".github/workflows/localsinglenode-maintenance.yml").read_text())
        job = workflow["jobs"]["prune-release-artifacts"]
        self.assertIn("17 3 * * *", job["if"])
        step = next(item for item in job["steps"]
                    if item.get("name") == "Keep the BuildKit cache under its budget under the shared deployment lock")
        self.assertIn("with-deploy-lock.sh python3 Deployment/LocalSingleNode/Scripts/lib/build_cache.py", step["run"])
        self.assertIn("--keep-gb", step["run"])
        names = [item.get("name") for item in job["steps"]]
        self.assertLess(names.index("Require green main CI for unchanged operation code"), names.index(step["name"]))

    def test_cache_is_also_trimmed_every_four_hours_under_the_same_guards(self):
        workflow = yaml.safe_load((ROOT / ".github/workflows/localsinglenode-maintenance.yml").read_text())
        crons = [entry["cron"] for entry in workflow[True]["schedule"]]
        self.assertIn("57 */4 * * *", crons)
        job = workflow["jobs"]["trim-build-cache"]
        self.assertEqual("github.event.schedule == '57 */4 * * *'", job["if"])
        names = [item.get("name") for item in job["steps"]]
        self.assertEqual(["Require this deployment target to be enabled", "Verify the intended native node",
                          "Require main branch", "Checkout", "Require green main CI for unchanged operation code",
                          "Keep the BuildKit cache under its budget under the shared deployment lock"], names)
        self.assertIn("with-deploy-lock.sh python3 Deployment/LocalSingleNode/Scripts/lib/build_cache.py --keep-gb",
                      job["steps"][-1]["run"])

    def test_image_retention_runs_daily_for_each_app_after_the_backup_timers(self):
        workflow = yaml.safe_load((ROOT / ".github/workflows/localsinglenode-maintenance.yml").read_text())
        crons = [entry["cron"] for entry in workflow[True]["schedule"]]
        self.assertIn("37 4 * * *", crons)
        self.assertIn("47 4 * * *", crons)
        job = workflow["jobs"]["maintenance"]
        self.assertIn("github.event.schedule == '37 4 * * *'", job["if"])
        self.assertIn("github.event.schedule == '47 4 * * *'", job["if"])
        check = next(item for item in job["steps"] if item.get("name") == "Check whether the selected app exists")
        self.assertIn("github.event.schedule == '47 4 * * *' && 'additional'", check["env"]["APP_INSTANCE"])
        step = next(item for item in job["steps"] if item.get("name") == "Protected backup and scoped maintenance")
        self.assertEqual("inputs.acceptance_only != true && steps.instance.outputs.configured == 'true'", step["if"])
        self.assertIn('if [[ "$BACKUP_NOW" == true ]]', step["run"])
        reboot = next(item for item in job["steps"] if item.get("name") == "Schedule completion-aware reboot check")
        self.assertIn("inputs.reboot_check == true", reboot["if"])
