"""The shared node setup bounds the system journal on every LocalCluster and LocalSingleNode node."""
from pathlib import Path
import unittest

import yaml

ROOT = Path(__file__).resolve().parents[4]
ROLE = ROOT / "Deployment/Common/ansible/roles/mint_base/tasks/main.yml"


class NodeJournalLimitTests(unittest.TestCase):
    def setUp(self):
        self.tasks = {task["name"]: task for task in yaml.safe_load(ROLE.read_text())}

    def test_journal_drop_in_bounds_size_and_keeps_free_space(self):
        task = self.tasks["Bound the system journal"]["ansible.builtin.copy"]
        self.assertEqual("/etc/systemd/journald.conf.d/50-grumplates-size.conf", task["dest"])
        lines = task["content"].splitlines()
        self.assertEqual("[Journal]", lines[0])
        self.assertIn("SystemMaxUse=1G", lines)
        self.assertIn("SystemKeepFree=5G", lines)
        self.assertEqual("0644", task["mode"])

    def test_journald_restarts_only_when_the_drop_in_changes(self):
        task = self.tasks["Apply the journal bound"]
        self.assertEqual({"name": "systemd-journald", "state": "restarted"}, task["ansible.builtin.systemd"])
        self.assertEqual("mint_base_journal_limit.changed", task["when"])
        self.assertEqual("mint_base_journal_limit", self.tasks["Bound the system journal"]["register"])

    def test_every_node_setup_playbook_applies_the_role(self):
        for playbook in ("Deployment/LocalCluster/ansible/playbooks/PrepareFreshLinuxMachine.yml",
                         "Deployment/LocalSingleNode/ansible/playbooks/PrepareSingleNode.yml"):
            plays = yaml.safe_load((ROOT / playbook).read_text())
            roles = [role if isinstance(role, str) else role.get("role") for play in plays for role in play.get("roles", [])]
            self.assertIn("mint_base", roles, playbook)


if __name__ == "__main__":
    unittest.main()
