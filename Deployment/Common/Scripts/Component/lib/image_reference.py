"""Parse Docker image references (repo:tag, repo@sha256:digest)."""

from __future__ import annotations


def repository_of(reference: str) -> str:
    """Return the repository of an image reference; a registry port stays part of it."""
    name = reference.split("@", 1)[0]
    parent, _, last = name.rpartition("/")
    last = last.split(":", 1)[0]
    return f"{parent}/{last}" if parent else last
