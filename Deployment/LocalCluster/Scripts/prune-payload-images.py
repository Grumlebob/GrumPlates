#!/usr/bin/env python3
"""Remove this app's old release payload images from node-main; read-only by default.

A deployment pulls the payload image, copies /release into a file pin and never needs the image
again, so nothing else removes it. On 2026-11-04 another app on node-main had kept 100 such images
(26.8 GB). An image is removed only when all of these hold:

- every reference (tags and digests) names this app's payload repository,
  <parent>/<release_payload_prefix><app>-release-payload;
- its io.github.release-payload.app-image label names this app's image;
- it is older than --until-hours (default 24);
- no container (running or stopped) uses it, and it is not a --protect-image reference.

Removal is by image ID without force, so a new tag or container makes Docker refuse. Applying
must run under with-deploy-lock.sh, so no deployment extracts a payload meanwhile. Exit codes:
0 done, 1 failure, 75 a removal was refused and left in place.
"""
from __future__ import annotations

import argparse
import importlib.util
import json
from datetime import datetime, timedelta, timezone
from pathlib import Path
import subprocess
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
REPO_ROOT = SCRIPT_DIR.parents[2]
APP_IMAGE_LABEL = "io.github.release-payload.app-image"


def _load_lock_check():
    spec = importlib.util.spec_from_file_location("prune_ci_residue", SCRIPT_DIR / "prune-ci-residue.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.require_lock


def run(*args: str, timeout: int = 120) -> subprocess.CompletedProcess:
    return subprocess.run(args, capture_output=True, text=True, timeout=timeout, check=False)


def release_setting(key: str) -> str:
    result = run("bash", str(REPO_ROOT / "Deployment/Common/Scripts/read-release-setting.sh"), key, timeout=30)
    if result.returncode:
        raise RuntimeError(f"release setting {key} is unavailable")
    return result.stdout.strip()


def payload_repository(app_image: str, prefix: str) -> str:
    parent, name = app_image.rsplit("/", 1)
    return f"{parent}/{prefix}{name}-release-payload"


def repository_of(reference: str) -> str:
    """repo:tag or repo@sha256:digest -> repo (a registry port stays part of the name)."""
    name = reference.split("@", 1)[0]
    last = name.rsplit("/", 1)[-1]
    if ":" in last:
        name = name[: len(name) - len(last)] + last.split(":", 1)[0]
    return name


def is_candidate(image: dict, repository: str, app_image: str, cutoff: datetime,
                 used_ids: set[str], protected: set[str]) -> bool:
    references = (image.get("RepoTags") or []) + (image.get("RepoDigests") or [])
    if not references or any(repository_of(ref) != repository for ref in references):
        return False
    if image.get("Id") in used_ids or protected.intersection(references) or image.get("Id") in protected:
        return False
    labels = (image.get("Config") or {}).get("Labels") or {}
    if labels.get(APP_IMAGE_LABEL, "").casefold() != app_image.casefold():
        return False
    created = datetime.fromisoformat(image["Created"].replace("Z", "+00:00"))
    return created < cutoff


class Docker:
    """Thin wrapper so tests can replace every Docker call."""

    def json(self, *args: str) -> list:
        result = run("docker", *args, timeout=300)
        if result.returncode:
            raise RuntimeError(f"docker {' '.join(args[:2])} failed: {result.stderr.strip()[:300]}")
        return json.loads(result.stdout or "[]")

    def lines(self, *args: str) -> list[str]:
        result = run("docker", *args, timeout=300)
        if result.returncode:
            raise RuntimeError(f"docker {' '.join(args[:2])} failed: {result.stderr.strip()[:300]}")
        return [line for line in result.stdout.splitlines() if line.strip()]

    def inspect_image(self, image_id: str) -> dict | None:
        """Return the image's current inspection, or None when it is gone."""
        result = run("docker", "image", "inspect", image_id)
        if result.returncode:
            return None
        rows = json.loads(result.stdout or "[]")
        return rows[0] if len(rows) == 1 else None

    def attempt(self, *args: str) -> tuple[bool, str]:
        result = run("docker", *args)
        return result.returncode == 0, (result.stderr or result.stdout).strip()[:300]


def inspect_all(docker: Docker, kind: str, ids: list[str]) -> list:
    rows = []
    for start in range(0, len(ids), 100):
        rows += docker.json(kind, "inspect", *ids[start:start + 100])
    return rows


def prune(docker: Docker, repository: str, app_image: str, now: datetime, until: timedelta,
          protected: set[str], apply: bool) -> dict:
    cutoff = now - until
    container_ids = docker.lines("ps", "-aq", "--no-trunc")
    used_ids = {row.get("Image") for row in inspect_all(docker, "container", container_ids)} if container_ids else set()
    image_ids = list(dict.fromkeys(docker.lines("image", "ls", "-aq", "--no-trunc")))
    images = inspect_all(docker, "image", image_ids) if image_ids else []
    counts = {"mode": "apply" if apply else "dry-run", "repository": repository,
              "candidates": 0, "removed": 0, "refused": 0}
    for image in images:
        if not is_candidate(image, repository, app_image, cutoff, used_ids, protected):
            continue
        counts["candidates"] += 1
        image_id = image["Id"]
        if not apply:
            print(f"would remove payload image {image_id}")
            continue
        # Recheck immediately before removal: the image and its container references may have changed.
        current = docker.inspect_image(image_id)
        if current is None:
            continue
        ok, used = docker.attempt("ps", "-aq", "--filter", "ancestor=" + image_id)
        if not ok or used or not is_candidate(current, repository, app_image, cutoff, set(), protected):
            print(f"left payload image {image_id}: it changed or gained a container reference")
            continue
        ok, text = docker.attempt("image", "rm", image_id)
        if ok:
            counts["removed"] += 1
            print(f"removed payload image {image_id}")
        else:
            counts["refused"] += 1
            print(f"refused without force; left payload image {image_id}: {text}", file=sys.stderr)
    return counts


def main(argv=None, docker: Docker | None = None, now: datetime | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--apply", action="store_true", help="Remove candidates (default: report only)")
    parser.add_argument("--until-hours", type=int, default=24, help="Keep payload images younger than this")
    parser.add_argument("--protect-image", action="append", default=[], help="Image reference or ID to keep")
    args = parser.parse_args(argv)
    if not 1 <= args.until_hours <= 24 * 90:
        parser.error("--until-hours must be between 1 and 2160")
    if args.apply:
        _load_lock_check()()
    app_image = release_setting("app_image")
    repository = payload_repository(app_image, release_setting("release_payload_prefix"))
    counts = prune(docker or Docker(), repository, app_image, now or datetime.now(timezone.utc),
                   timedelta(hours=args.until_hours), set(args.protect_image), args.apply)
    print(json.dumps(counts), flush=True)
    return 75 if counts["refused"] else 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (RuntimeError, ValueError, OSError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
