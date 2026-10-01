# Single entry point for every local and CI task. `just` lists the recipes.
# Extra arguments are forwarded to the underlying tool via "$@".
set positional-arguments
set dotenv-load
set shell := ["bash", "-euo", "pipefail", "-c"]

sln := "RecipeJoe.slnx"
# Its own compose project, so E2E never touches the dev database and runs alongside `just dev`.
e2e_compose := "docker compose -p recipejoe-e2e -f compose.yaml -f compose.e2e.yaml --profile app --profile fixtures"
prettier := justfile_directory() / "frontend/node_modules/.bin/prettier"
# Root-level YAML/JSON (compose, CI, Renovate, Claude settings), formatted with the frontend's Prettier.
root_globs := "*.{json,yml,yaml} .github/**/*.{yml,yaml} .claude/*.json docker/**/*.{json,yml,yaml}"

[default]
list:
    @just --list --unsorted

# First-time setup on a fresh clone.
[group('setup')]
setup:
    [ -f .env ] || cp .env.example .env
    lefthook install
    cd frontend && npm ci
    cd e2e && npm ci
    cd backend && dotnet restore {{ sln }} --locked-mode
    cd backend && dotnet tool restore

# Postgres + API with hot reload (:5080) + Vite; Ctrl-C stops all three, data survives.
[group('develop')]
dev:
    #!/usr/bin/env bash
    set -euo pipefail
    docker compose up -d --wait postgres
    (cd frontend && exec node_modules/.bin/vite) &
    web=$!
    trap 'kill $web 2>/dev/null || true; docker compose stop postgres' EXIT
    # The API runs in the foreground: background jobs ignore SIGINT, so only it gets Ctrl-C; the trap stops the rest.
    # The API runs on the host here, where host.docker.internal doesn't resolve: local Ollama is on localhost.
    if [ "${Llm__Provider:-Ollama}" = Ollama ]; then export Llm__BaseUrl="${Llm__BaseUrl:-http://localhost:11434}"; fi
    cd backend
    ConnectionStrings__Db="Host=localhost;Port=${POSTGRES_PORT:-5432};Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD" \
        dotnet watch --project src/RecipeJoe.Api

# Import every fixture recipe into an empty dev Library (needs `just dev`); fixtures run only meanwhile.
[group('develop')]
seed:
    docker compose --profile fixtures up -d --wait fixtures
    trap 'docker compose --profile fixtures stop fixtures' EXIT; scripts/seed.sh

# Build and (re)start postgres, backend and web (http://localhost:8080).
[group('full stack')]
up:
    docker compose --profile app up -d --build --wait

# Remove the dev containers (the database volume stays); `just down -v` also deletes it.
[group('full stack')]
down *args:
    docker compose --profile app --profile fixtures down "$@"

# Build backend and frontend.
[group('build / format / lint')]
build:
    cd backend && dotnet build {{ sln }}
    cd frontend && npm run build

# Fix formatting everywhere.
[group('build / format / lint')]
fmt:
    cd backend && dotnet format {{ sln }}
    cd frontend && npm run format
    cd e2e && npm run format
    {{ prettier }} --write --ignore-unknown --no-error-on-unmatched-pattern {{ root_globs }}

# Verify formatting everywhere (no changes).
[group('build / format / lint')]
fmt-check:
    cd backend && dotnet format {{ sln }} --verify-no-changes
    cd frontend && npm run format:check
    cd e2e && npm run format:check
    {{ prettier }} --check --ignore-unknown --no-error-on-unmatched-pattern {{ root_globs }}

# ESLint, tsc and the backend build with analyzers as errors.
[group('build / format / lint')]
lint:
    cd frontend && npm run lint && npm run typecheck
    cd e2e && npm run lint && npm run typecheck
    cd backend && dotnet build {{ sln }} --no-incremental -warnaserror

# Every layer.
[group('tests')]
test: test-unit test-int test-contract test-e2e

# Backend unit tests (extra args go to dotnet test) + Vitest.
[group('tests')]
[working-directory('backend')]
test-unit *args:
    dotnet test --project tests/RecipeJoe.UnitTests "$@"
    cd ../frontend && npm test

# Backend integration tests; Testcontainers starts its own Postgres.
[group('tests')]
[working-directory('backend')]
test-int *args:
    dotnet test --project tests/RecipeJoe.IntegrationTests "$@"

# OpenAPI/TS client drift and pending EF model changes.
[group('tests')]
[working-directory('backend')]
test-contract *args:
    dotnet test --project tests/RecipeJoe.ContractTests "$@"
    cd ../frontend && npm run check:api

# Fresh isolated stack plus the fixtures site (web on :8090), then Playwright against it.
[group('tests')]
test-e2e *args:
    {{ e2e_compose }} down -v
    {{ e2e_compose }} up -d --build --wait
    cd e2e && BASE_URL="http://localhost:${E2E_WEB_PORT:-8090}" npx playwright test "$@"

# Remove the E2E stack and its database.
[group('tests')]
down-e2e:
    {{ e2e_compose }} down -v

# Unit + integration tests with merged coverage; 80 % line gate only when CI is set (extra args go to dotnet test).
[group('coverage')]
cov-backend *args:
    scripts/coverage.sh "$@"

[group('coverage')]
[working-directory('frontend')]
cov-frontend:
    npm run cov

# Stryker.NET (unit tests) + StrykerJS; local only.
[group('mutation testing')]
mutate:
    cd backend/tests/RecipeJoe.UnitTests && dotnet dotnet-stryker
    cd frontend && npx stryker run
