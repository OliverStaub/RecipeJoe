# Single entry point for every local and CI task. `just` lists the recipes.
# Extra arguments are forwarded to the underlying tool via "$@".
set positional-arguments
set dotenv-load
set shell := ["bash", "-euo", "pipefail", "-c"]

sln := "RecipeJoe.slnx"
prettier := justfile_directory() / "frontend/node_modules/.bin/prettier"
# Root-level YAML/JSON (compose, CI, Renovate, Claude settings), formatted with the frontend's Prettier.
root_globs := "*.{json,yml,yaml} .github/**/*.{yml,yaml} .claude/*.json docker/**/*.{json,yml,yaml}"

[default]
list:
    @just --list --unsorted

# --- setup -------------------------------------------------------------------

# First-time setup on a fresh clone.
setup:
    [ -f .env ] || cp .env.example .env
    lefthook install
    cd frontend && npm ci
    cd e2e && npm ci
    cd backend && dotnet restore {{ sln }} --locked-mode
    cd backend && dotnet tool restore

# --- develop -----------------------------------------------------------------

# Postgres + fixtures site for local development.
dev-db:
    docker compose up -d --wait postgres fixtures

# API with hot reload against the dev database (http://localhost:5080).
[working-directory('backend')]
dev-api *args:
    ConnectionStrings__Db="Host=localhost;Port=${POSTGRES_PORT:-5432};Database=$POSTGRES_DB;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD" \
        dotnet watch --project src/RecipeJoe.Api "$@"

# Vite dev server, proxies /api to the API.
[working-directory('frontend')]
dev-web *args:
    npm run dev -- "$@"

# Import every fixture recipe into an empty dev Library (needs dev-db and dev-api).
seed:
    scripts/seed.sh

# --- full stack --------------------------------------------------------------

# Build and start postgres, fixtures, backend and web.
up:
    docker compose --profile app up -d --build --wait

# Stop the full stack.
down:
    docker compose --profile app down

# Stop everything and delete the database volume.
reset:
    docker compose --profile app down -v

# --- build / format / lint ---------------------------------------------------

# Build backend and frontend.
build:
    cd backend && dotnet build {{ sln }}
    cd frontend && npm run build

# Fix formatting everywhere.
fmt:
    cd backend && dotnet format {{ sln }}
    cd frontend && npm run format
    cd e2e && npm run format
    {{ prettier }} --write --ignore-unknown --no-error-on-unmatched-pattern {{ root_globs }}

# Verify formatting everywhere (no changes).
fmt-check:
    cd backend && dotnet format {{ sln }} --verify-no-changes
    cd frontend && npm run format:check
    cd e2e && npm run format:check
    {{ prettier }} --check --ignore-unknown --no-error-on-unmatched-pattern {{ root_globs }}

# ESLint, tsc and the backend build with analyzers as errors.
lint:
    cd frontend && npm run lint && npm run typecheck
    cd e2e && npm run lint && npm run typecheck
    cd backend && dotnet build {{ sln }} --no-incremental -warnaserror

# --- tests -------------------------------------------------------------------

# Every layer.
test: test-unit test-int test-contract test-e2e

# Backend unit tests (extra args go to dotnet test) + Vitest.
[working-directory('backend')]
test-unit *args:
    dotnet test --project tests/RecipeJoe.UnitTests "$@"
    cd ../frontend && npm test

# Backend integration tests; Testcontainers starts its own Postgres.
[working-directory('backend')]
test-int *args:
    dotnet test --project tests/RecipeJoe.IntegrationTests "$@"

# OpenAPI/TS client drift and pending EF model changes.
[working-directory('backend')]
test-contract *args:
    dotnet test --project tests/RecipeJoe.ContractTests "$@"
    cd ../frontend && npm run check:api

# Fresh stack, then Playwright against it.
test-e2e *args: reset up
    cd e2e && npx playwright test "$@"

# --- coverage ----------------------------------------------------------------

# Tests with coverage. The 80 % line gate applies only when CI is set: `CI=1 just cov`.
cov: cov-backend cov-frontend

# Unit + integration coverage merged by ReportGenerator.
[working-directory('backend')]
cov-backend:
    rm -rf TestResults/coverage CoverageReport
    for project in UnitTests IntegrationTests; do \
        dotnet test --project tests/RecipeJoe.$project \
            --coverage --coverage-output-format cobertura \
            --coverage-settings CodeCoverage.config \
            --results-directory TestResults/coverage; \
    done
    dotnet reportgenerator \
        -reports:"TestResults/coverage/*.cobertura.xml" \
        -targetdir:CoverageReport \
        -reporttypes:"TextSummary;MarkdownSummaryGithub"
    cat CoverageReport/Summary.txt
    if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then cat CoverageReport/SummaryGithub.md >> "$GITHUB_STEP_SUMMARY"; fi
    # ReportGenerator's threshold option is PRO-only, so the gate is checked here.
    if [ -n "${CI:-}" ]; then \
        line=$(sed -n 's/^ *Line coverage: *\([0-9.]*\)%.*/\1/p' CoverageReport/Summary.txt); \
        awk -v c="$line" 'BEGIN { exit !(c != "" && c >= 80) }' \
            || { echo "Line coverage ${line:-unknown}% is below the 80% gate" >&2; exit 1; }; \
    fi

[working-directory('frontend')]
cov-frontend:
    npm run cov

# --- mutation testing (local only) -------------------------------------------

# Stryker.NET (unit tests) + StrykerJS.
mutate:
    cd backend/tests/RecipeJoe.UnitTests && dotnet dotnet-stryker
    cd frontend && npx stryker run
