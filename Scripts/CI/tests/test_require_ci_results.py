from __future__ import annotations

import contextlib
import io
import os
import unittest
from unittest.mock import patch

from Scripts.CI import require_ci_results as gate


def valid(*, event="pull_request", ref="refs/pull/17/merge", **changes):
    results = dict(validation="success", publication="skipped")
    if event in {"push", "workflow_dispatch"} and ref == "refs/heads/main":
        results.update(publication="success")
    results.update(changes)
    gate.validate_required_results(event_name=event, ref=ref, **results)


class RequiredResultTests(unittest.TestCase):
    def test_pull_request_and_branch_dispatch_require_validation_only(self):
        valid()
        valid(event="workflow_dispatch", ref="refs/heads/feature")

    def test_main_push_and_main_dispatch_require_publication(self):
        valid(event="push", ref="refs/heads/main")
        valid(event="workflow_dispatch", ref="refs/heads/main")

    def test_validation_failure_fails(self):
        for result in ("failure", "cancelled", "skipped", "neutral"):
            with self.subTest(result=result), self.assertRaises(gate.RequiredResultError):
                valid(validation=result)

    def test_main_publication_must_succeed(self):
        for result in ("failure", "cancelled", "skipped", "neutral"):
            with self.subTest(result=result), self.assertRaises(gate.RequiredResultError):
                valid(event="push", ref="refs/heads/main", publication=result)

    def test_publication_must_stay_skipped_for_pull_requests_and_branch_dispatches(self):
        for event, ref in (("pull_request", "refs/pull/17/merge"),
                           ("workflow_dispatch", "refs/heads/feature")):
            with self.subTest(event=event), self.assertRaises(gate.RequiredResultError):
                valid(event=event, ref=ref, publication="success")

    def test_unexpected_event_or_ref_fails_closed(self):
        for event, ref in (("push", "refs/heads/feature"), ("pull_request", "refs/heads/main"),
                           ("workflow_run", "refs/heads/main"), ("workflow_dispatch", "")):
            with self.subTest(event=event, ref=ref), self.assertRaises(gate.RequiredResultError):
                valid(event=event, ref=ref)

    def test_cli_passes_and_fails_without_echoing_environment_values(self):
        base = dict(CI_EVENT_NAME="pull_request", CI_REF="refs/pull/17/merge",
                    CI_VALIDATION_RESULT="success", CI_PUBLICATION_RESULT="skipped")
        output, errors = io.StringIO(), io.StringIO()
        with (patch.dict(os.environ, base, clear=True), contextlib.redirect_stdout(output),
              contextlib.redirect_stderr(errors)):
            self.assertEqual(0, gate.main())
        self.assertIn("passed", output.getvalue())
        self.assertEqual("", errors.getvalue())

        output, errors = io.StringIO(), io.StringIO()
        with (patch.dict(os.environ, base | dict(CI_VALIDATION_RESULT="failure"), clear=True),
              contextlib.redirect_stdout(output), contextlib.redirect_stderr(errors)):
            self.assertEqual(1, gate.main())
        self.assertEqual("", output.getvalue())
        self.assertIn("Required CI result gate failed", errors.getvalue())
        self.assertNotIn("refs/pull/17/merge", errors.getvalue())


if __name__ == "__main__":
    unittest.main()
