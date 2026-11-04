#!/usr/bin/env python3
"""Payload image retention on node-main, with a fake Docker; no live deletions."""
from datetime import datetime, timedelta, timezone
import importlib.util
from pathlib import Path
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location(
    "prune_payload_images", Path(__file__).resolve().parents[1] / "prune-payload-images.py"
)
module = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(module)

APP = "ghcr.io/fixture-owner/books"
REPO = "ghcr.io/fixture-owner/private-books-release-payload"
NOW = datetime(2026, 11, 4, 12, tzinfo=timezone.utc)
OLD = (NOW - timedelta(hours=25)).isoformat()


def payload(image_id, *refs, created=OLD, app=APP):
    return {"Id": image_id, "Created": created,
            "RepoTags": [r for r in refs if "@" not in r],
            "RepoDigests": [r for r in refs if "@" in r],
            "Config": {"Labels": {module.APP_IMAGE_LABEL: app}}}


class FakeDocker:
    def __init__(self, images, containers=(), refuse=(), ancestors=()):
        self.images = {image["Id"]: image for image in images}
        self.containers = list(containers)
        self.refuse = set(refuse)
        self.ancestors = set(ancestors)
        self.removed = []

    def lines(self, *args):
        if args[:2] == ("ps", "-aq"):
            return [f"c{i}" for i, _ in enumerate(self.containers)]
        if args[:2] == ("image", "ls"):
            return list(self.images)
        raise AssertionError(args)

    def json(self, kind, verb, *ids):
        if kind == "container":
            return [{"Id": f"c{i}", "Image": image} for i, image in enumerate(self.containers)]
        return [self.images[i] for i in ids]

    def inspect_image(self, image_id):
        return self.images.get(image_id)

    def attempt(self, *args):
        if args[:2] == ("ps", "-aq"):
            image_id = args[-1].split("=", 1)[1]
            return True, "c9" if image_id in self.ancestors else ""
        if args[:2] == ("image", "rm"):
            assert "--force" not in args and "-f" not in args
            if args[2] in self.refuse:
                return False, "conflict: image is referenced in multiple repositories"
            self.removed.append(args[2])
            del self.images[args[2]]
            return True, ""
        raise AssertionError(args)


def run(docker, apply=True, protected=()):
    return module.prune(docker, REPO, APP, NOW, timedelta(hours=24), set(protected), apply)


class PayloadImageTests(unittest.TestCase):
    def test_repository_follows_the_release_prefix(self):
        self.assertEqual(REPO, module.payload_repository(APP, "private-"))
        self.assertEqual("ghcr.io/fixture-owner/catalog-release-payload",
                         module.payload_repository("ghcr.io/fixture-owner/catalog", ""))

    def test_only_old_owned_unused_unprotected_payloads_are_removed(self):
        digest = "@sha256:" + "a" * 64
        docker = FakeDocker([
            payload("pulled", REPO + digest),
            payload("tagged", REPO + ":run-1-attempt-1", REPO + digest.replace("a", "b")),
            payload("recent", REPO + ":run-2", created=(NOW - timedelta(hours=23)).isoformat()),
            payload("in-use", REPO + ":run-3"),
            payload("protected", REPO + ":run-4"),
            payload("foreign-label", REPO + ":run-5", app="ghcr.io/other/app"),
            payload("other-app", "ghcr.io/fixture-owner/private-books-react-release-payload:run-6"),
            payload("mixed", REPO + ":run-7", APP + ":sha"),
            payload("app-image", APP + ":old"),
            payload("untagged", created=OLD),
        ], containers=["in-use"])
        counts = run(docker, protected={REPO + ":run-4"})
        self.assertEqual(["pulled", "tagged"], docker.removed)
        self.assertEqual({"candidates": 2, "removed": 2, "refused": 0}, {k: counts[k] for k in ("candidates", "removed", "refused")})

    def test_dry_run_never_removes(self):
        docker = FakeDocker([payload("pulled", REPO + ":run-1")])
        counts = run(docker, apply=False)
        self.assertEqual([], docker.removed)
        self.assertEqual(1, counts["candidates"])

    def test_a_new_container_reference_or_refusal_leaves_the_image(self):
        docker = FakeDocker([payload("raced", REPO + ":run-1"), payload("refused", REPO + ":run-2"),
                             payload("next", REPO + ":run-3")], ancestors={"raced"}, refuse={"refused"})
        counts = run(docker)
        self.assertEqual(["next"], docker.removed)
        self.assertEqual(1, counts["refused"])

    def test_apply_requires_the_deployment_lock(self):
        def refuse_lock():
            raise RuntimeError("run under with-deploy-lock.sh; the deployment lock is not held")
        with patch.object(module, "_load_lock_check", return_value=refuse_lock):
            with self.assertRaisesRegex(RuntimeError, "deployment lock"):
                module.main(["--apply"], docker=FakeDocker([]))

    def test_dry_run_exit_code_and_refusal_exit_code(self):
        with patch.object(module, "release_setting", side_effect=lambda key: {"app_image": APP, "release_payload_prefix": "private-"}[key]):
            self.assertEqual(0, module.main([], docker=FakeDocker([payload("p", REPO + ":run-1")]), now=NOW))
            with patch.object(module, "_load_lock_check", return_value=lambda: None):
                self.assertEqual(75, module.main(["--apply"], docker=FakeDocker([payload("p", REPO + ":run-1")], refuse={"p"}), now=NOW))


if __name__ == "__main__":
    unittest.main()
