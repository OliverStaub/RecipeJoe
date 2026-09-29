#!/usr/bin/env bash
# Tests for format-file.sh with stubbed dotnet/npx. Run: bash .claude/hooks/test-format-file.sh
set -uo pipefail

HOOK="$(cd "$(dirname "$0")" && pwd)/format-file.sh"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

R="$tmp/repo"
mkdir -p "$tmp/bin" "$R/backend/src" "$R/frontend/src" "$R/e2e/tests" "$R/docs"
git init -q -b main "$R" || { echo "setup failed"; exit 1; }
for f in backend/src/A.cs frontend/src/a.ts frontend/src/b.tsx e2e/tests/s.ts docs/x.md; do : >"$R/$f"; done

# Stubs log "<cwd-basename>: <args>". ESLINT_RC / PRETTIER_RC / DOTNET_RC set exit codes.
cat >"$tmp/bin/dotnet" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$PWD"): dotnet $*" >>"$CALLS"
exit "${DOTNET_RC:-0}"
STUB
cat >"$tmp/bin/npx" <<'STUB'
#!/usr/bin/env bash
echo "$(basename "$PWD"): $*" >>"$CALLS"
if [ "$2" = eslint ]; then echo "eslint says no"; exit "${ESLINT_RC:-0}"; fi
exit "${PRETTIER_RC:-0}"
STUB
chmod +x "$tmp/bin/dotnet" "$tmp/bin/npx"
export PATH="$tmp/bin:$PATH" CALLS="$tmp/calls"

pass=0
fail=0
# check <name> <expected exit> <expected calls (newline-separated)> <file> [ENV=val ...]
check() {
  local name=$1 want_rc=$2 want_calls=$3 file=$4 rc
  shift 4
  : >"$CALLS"
  LAST_ERR=$(jq -n --arg f "$file" '{tool_input:{file_path:$f}}' | env "$@" bash "$HOOK" 2>&1 >/dev/null)
  rc=$?
  if [ "$rc" = "$want_rc" ] && [ "$(cat "$CALLS")" = "$want_calls" ]; then
    pass=$((pass + 1))
  else
    fail=$((fail + 1))
    echo "FAIL: $name | rc=$rc (want $want_rc) | calls: $(tr '\n' ';' <"$CALLS")"
  fi
}

# js_calls <package> <path within package>: the expected ESLint-then-Prettier calls.
js_calls() {
  echo "$1: --no-install eslint --fix --no-warn-ignored $2"
  echo "$1: --no-install prettier --write --ignore-unknown --log-level warn $2"
}
JS_A=$(js_calls frontend src/a.ts)

check "cs: whitespace format, relative include" 0 \
  "backend: dotnet format whitespace . --folder --include src/A.cs" "$R/backend/src/A.cs"
check "cs: dotnet failure is silent" 0 \
  "backend: dotnet format whitespace . --folder --include src/A.cs" "$R/backend/src/A.cs" DOTNET_RC=1
check "ts: eslint then prettier" 0 "$JS_A" "$R/frontend/src/a.ts"
check "tsx handled" 0 "$(js_calls frontend src/b.tsx)" "$R/frontend/src/b.tsx"
check "e2e ts uses e2e package" 0 "$(js_calls e2e tests/s.ts)" "$R/e2e/tests/s.ts"
check "unfixable eslint: exit 2, prettier still runs" 2 "$JS_A" "$R/frontend/src/a.ts" ESLINT_RC=1
case $LAST_ERR in
  *"ESLint errors in frontend/src/a.ts"*"eslint says no"*) pass=$((pass + 1)) ;;
  *) fail=$((fail + 1)); echo "FAIL: stderr lacks eslint output: $LAST_ERR" ;;
esac
check "eslint tool failure (rc 2) is silent" 0 "$JS_A" "$R/frontend/src/a.ts" ESLINT_RC=2
check "prettier failure is silent" 0 "$JS_A" "$R/frontend/src/a.ts" PRETTIER_RC=2
check "other extension ignored" 0 "" "$R/docs/x.md"
check "missing file ignored" 0 "" "$R/frontend/src/nope.ts"
check "outside a repo ignored" 0 "" "$tmp/bin/dotnet"

# No file_path in the payload
: >"$CALLS"
echo '{}' | bash "$HOOK"
rc=$?
if [ "$rc" = 0 ] && [ ! -s "$CALLS" ]; then pass=$((pass + 1)); else fail=$((fail + 1)); echo "FAIL: no file_path"; fi

echo "passed=$pass failed=$fail"
[ "$fail" = 0 ]
