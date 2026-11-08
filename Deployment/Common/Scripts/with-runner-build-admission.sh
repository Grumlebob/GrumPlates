#!/usr/bin/env bash
# Admit one resource-heavy CI command on the shared native node.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail() {
  echo "runner build admission failed: $*" >&2
  exit 1
}

usage() {
  echo "usage: $0 --pool native|desktop <command> [args...]" >&2
  echo "       $0 --runner-label <localsinglenode|localcluster label> <command> [args...]" >&2
  exit 2
}

[[ $# -ge 3 ]] || usage
route="$1"
value="$2"
shift 2
[[ $# -gt 0 ]] || usage

admit=false
case "$route" in
  --pool)
    case "$value" in
      native) admit=true ;;
      desktop) admit=false ;;
      *) fail "unsupported runner pool '$value'" ;;
    esac
    ;;
  --runner-label)
    case "$value" in
      localsinglenode|localsinglenode-*) admit=true ;;
      localcluster|localcluster-*) admit=false ;;
      *) fail "unsupported runner label '$value'" ;;
    esac
    ;;
  *) usage ;;
esac

if [[ "$admit" == true ]]; then
  exec bash "$SCRIPT_DIR/with-node-browser-lock.sh" "$@"
fi
exec "$@"
