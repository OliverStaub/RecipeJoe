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
if [ "$(curl -fsS "$api/api/imports" | jq length)" -ne 0 ]; then
    echo "seed: unfinished Imports exist; dismiss them or start fresh with 'just down -v && just dev'" >&2
    exit 1
fi

total=0
for file in "$root"/fixtures/recipes/*.html; do
    total=$((total + 1))
    name=$(basename "$file" .html)
    body=$(jq -n --arg url "$fixtures_url/recipes/$name.html" '{url: $url}')
    curl -fsS -o /dev/null -H 'Content-Type: application/json' -d "$body" "$api/api/imports" \
        || { echo "✗ $name (not started)" >&2; exit 1; }
done

# Imports run in the background; a finished one leaves the list, a failed one stays as Failed.
for _ in $(seq 120); do
    imports=$(curl -fsS "$api/api/imports")
    [ "$(jq '[.[] | select(.state == "Pending")] | length' <<<"$imports")" -eq 0 ] && break
    sleep 0.5
done
jq -r '.[] | "✗ \(.url) (\(.failure // .state))"' <<<"$imports"
failed=$(jq length <<<"$imports")

echo "seed: $((total - failed))/$total imported"
[ "$failed" -eq 0 ]
