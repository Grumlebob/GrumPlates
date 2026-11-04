from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "Component" / "lib"))

from image_reference import repository_of  # noqa: E402

DIGEST = "sha256:" + "a" * 64


class RepositoryOfTests(unittest.TestCase):
    def test_tag_and_digest_references_name_their_repository(self):
        for reference in ("ghcr.io/owner/books:run-1", f"ghcr.io/owner/books@{DIGEST}",
                          f"ghcr.io/owner/books:run-1@{DIGEST}", "ghcr.io/owner/books"):
            self.assertEqual("ghcr.io/owner/books", repository_of(reference), reference)

    def test_registry_port_stays_part_of_the_repository(self):
        self.assertEqual("localhost:5000/books", repository_of("localhost:5000/books:run-1"))
        self.assertEqual("localhost:5000/books", repository_of(f"localhost:5000/books@{DIGEST}"))

    def test_bare_names(self):
        self.assertEqual("redis", repository_of("redis:7"))
        self.assertEqual("redis", repository_of("redis"))


if __name__ == "__main__":
    unittest.main()
