#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ADMISSION="$SCRIPT_DIR/with-runner-build-admission.sh"
LOCK="$SCRIPT_DIR/with-node-browser-lock.sh"
TMP="$(mktemp -d)"
trap 'rm -rf -- "$TMP"' EXIT
export LOCALSINGLENODE_BROWSER_LOCK_FILE="$TMP/browser.lock"

fail() {
  echo "test-with-runner-build-admission: $*" >&2
  exit 1
}

# Native admission uses the shared lock and preserves the command's exit status.
set +e
bash "$ADMISSION" --pool native bash -c 'exit 23'
status=$?
set -e
[[ "$status" == 23 ]] || fail "expected native command exit 23, got $status"

# A complete native shell block keeps its environment and GitHub output writes.
export GITHUB_OUTPUT="$TMP/github-output"
bash "$ADMISSION" --runner-label localsinglenode bash -euo pipefail <<'ADMITTED'
echo 'admission-output=ok' >> "$GITHUB_OUTPUT"
ADMITTED
grep -q '^admission-output=ok$' "$GITHUB_OUTPUT" || fail "admitted shell block lost its output file write"

# Native canary labels take the same lock; desktop and cluster routes stay direct.
bash "$ADMISSION" --runner-label localsinglenode-books true
bash "$ADMISSION" --runner-label localsinglenode true
bash "$ADMISSION" --pool desktop true
bash "$ADMISSION" --runner-label localcluster-books true
bash "$ADMISSION" --runner-label localcluster true

# A held lock blocks native admission with the existing bounded timeout.
bash "$LOCK" bash -c "touch '$TMP/held'; sleep 4" &
holder=$!
for _ in $(seq 1 100); do
  [[ -e "$TMP/held" ]] && break
  sleep 0.1
done
[[ -e "$TMP/held" ]] || fail "the lock holder never started"
bash "$ADMISSION" --pool desktop bash -c "touch '$TMP/desktop-ran'" || fail "desktop route failed"
bash "$ADMISSION" --runner-label localcluster-books bash -c "touch '$TMP/cluster-ran'" || fail "cluster route failed"
[[ -e "$TMP/desktop-ran" && -e "$TMP/cluster-ran" ]] || fail "desktop or cluster route waited for native admission"
set +e
LOCALSINGLENODE_BROWSER_LOCK_TIMEOUT_SECONDS=1 bash "$ADMISSION" --runner-label localsinglenode-books true 2>"$TMP/timeout.err"
status=$?
set -e
[[ "$status" == 1 ]] || fail "expected native lock timeout exit 1, got $status"
grep -q "timed out after 1s" "$TMP/timeout.err" || fail "native timeout message is missing"
wait "$holder" || fail "the lock holder should finish normally"

# Cancellation releases the lock so the next native command can proceed.
setsid bash "$ADMISSION" --pool native bash -c 'touch "$1"; exec sleep 30' _ "$TMP/cancel-started" &
holder=$!
for _ in $(seq 1 100); do
  [[ -e "$TMP/cancel-started" ]] && break
  sleep 0.1
done
[[ -e "$TMP/cancel-started" ]] || fail "the cancellable native command never started"
kill -TERM -- "-$holder"
set +e
wait "$holder"
status=$?
set -e
[[ "$status" != 0 ]] || fail "cancelled native command unexpectedly succeeded"
if kill -0 -- "-$holder" 2>/dev/null; then
  fail "cancelled native process group is still running"
fi
bash "$ADMISSION" --pool native true || fail "native lock was not released after cancellation"

# Unknown routes fail closed, and no command is never treated as success.
set +e
bash "$ADMISSION" --pool unknown true >/dev/null 2>&1
route_status=$?
bash "$ADMISSION" --pool native >/dev/null 2>&1
usage_status=$?
set -e
[[ "$route_status" == 1 ]] || fail "unknown pool must fail closed"
[[ "$usage_status" == 2 ]] || fail "missing command must show usage"

echo "runner build admission behaviour tests passed"
