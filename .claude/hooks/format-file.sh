#!/usr/bin/env bash
# PostToolUse: format the single file Claude just edited.
# exit 0 = fine/ignored (incl. tool failures); exit 2 = show stderr to Claude (unfixable ESLint errors).
set -uo pipefail
file=$(jq -r '.tool_input.file_path // empty')
[[ -n "$file" && -f "$file" ]] || exit 0
dir=$(cd "$(dirname "$file")" && pwd -P) || exit 0 # git reports symlink-resolved paths
file=$dir/$(basename "$file")
root=$(git -C "$dir" rev-parse --show-toplevel 2>/dev/null) || exit 0
rel=${file#"$root"/}

case "$rel" in
  backend/*.cs)
    cd "$root/backend" || exit 0
    # Whitespace only, no project load (~0.8s). Style/analyzer fixes happen in pre-commit.
    dotnet format whitespace . --folder --include "${rel#backend/}" >/dev/null 2>&1
    ;;
  frontend/*.ts | frontend/*.tsx | frontend/*.js | frontend/*.jsx | frontend/*.mjs | frontend/*.cjs | \
    e2e/*.ts | e2e/*.tsx | e2e/*.js | e2e/*.jsx | e2e/*.mjs | e2e/*.cjs)
    pkg=${rel%%/*}
    f=${rel#"$pkg"/}
    cd "$root/$pkg" || exit 0
    out=$(npx --no-install eslint --fix --no-warn-ignored "$f" 2>&1)
    rc=$?
    npx --no-install prettier --write --ignore-unknown --log-level warn "$f" >/dev/null 2>&1
    if [[ $rc -eq 1 ]]; then
      printf 'ESLint errors in %s:\n%s\n' "$rel" "$out" >&2
      exit 2
    fi
    ;;
esac
exit 0
