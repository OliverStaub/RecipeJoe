#!/usr/bin/env bash
# Dev only: import every fixture recipe through the running API (never run automatically).
# Run via `just seed` (starts fixtures on :8081) while `just dev` is running.
set -euo pipefail

api="${SEED_API:-http://localhost:${API_PORT:-5080}}"
fixtures_url="${SEED_FIXTURES_URL:-http://localhost:8081}"
root="$(cd "$(dirname "$0")/.." && pwd)"

existing=$(curl -fsS "$api/api/recipes" | jq length) \
    || { echo "seed: API not reachable at $api (run 'just dev')" >&2; exit 1; }
if [ "$existing" -ne 0 ]; then
    echo "seed: Library already has $existing recipes; start fresh with 'just down -v && just dev'" >&2
    exit 1
fi

tmp=$(mktemp)
trap 'rm -f "$tmp"' EXIT
total=0
failed=0
for file in "$root"/fixtures/recipes/*.html; do
    total=$((total + 1))
    name=$(basename "$file" .html)
    body=$(jq -n --arg url "$fixtures_url/recipes/$name.html" '{url: $url}')
    status=$(curl -sS -o "$tmp" -w '%{http_code}' \
        -H 'Content-Type: application/json' -d "$body" "$api/api/recipes/import") || status=000
    if [ "$status" = 201 ]; then
        echo "✓ $name"
    else
        echo "✗ $name ($(jq -r --arg s "HTTP $status" '.kind // $s' "$tmp" 2>/dev/null || echo "HTTP $status"))"
        failed=$((failed + 1))
    fi
done

echo "seed: $((total - failed))/$total imported"
[ "$failed" -eq 0 ]
