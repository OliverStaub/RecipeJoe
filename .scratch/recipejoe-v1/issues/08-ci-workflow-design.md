# CI workflow design

Type: grilling
Status: resolved
Blocked by: 03, 04, 05, 13

## Question

How are the GitHub Actions workflows structured to run the agreed stages (lint/format → build → unit → integration → coverage gate → E2E)? Jobs vs steps, parallel backend/frontend lanes, `needs` graph, caching, browser matrix, coverage merge + job summary, artifacts kept (test reports, Playwright traces), triggers (push to main, PRs, nightly?), runner-minute budget.
- From [V1 screens](09-v1-screens.md): the Cook View uses the Screen Wake Lock API, which only exists in a secure context. If E2E browsers reach the app at a non-localhost http origin (e.g. `http://web` on the compose network), `navigator.wakeLock` is undefined. Either serve E2E on `localhost` or assert only the "unavailable" fallback there.

## Comments

- From [Local automation choices](13-local-automation-choices.md): the CI format step is `just fmt-check`. It covers backend, frontend and root YAML/JSON via `frontend/node_modules/.bin/prettier`, so `npm ci` has to run before it. Local setup is `brew install lefthook just` + `just setup`. CI doesn't use lefthook.

## Answer

- **Triggers**: push to `main`, `pull_request`, `workflow_dispatch`. No nightly. `concurrency` group, `cancel-in-progress` on PRs.
- **Files**: single `.github/workflows/ci.yml`, no `paths:` filters.
- **Job graph**: `static` → (`backend-tests` ∥ `frontend-tests`) → `e2e`.
  - `static`: `just fmt-check`, `just lint`, `just build` (new recipe), `just test-contract` (OpenAPI/TS drift + EF `has-pending-model-changes`).
  - `backend-tests`: `just test-unit`, `just test-int`, `just cov` (backend).
  - `frontend-tests`: Vitest + `just cov` (frontend).
  - `e2e`: copy `.env.example` → `.env`, `just test-e2e`, `just down` under `if: always()`.
- **Build reuse**: none. Each job rebuilds from warm caches; no build artifacts passed between jobs.
- **E2E**: one job, 3 Playwright projects (Chromium, WebKit/iPhone, Pixel), no GitHub matrix. Playwright runs on the runner host against `http://localhost:8080` (secure context, so Wake Lock works); the backend reaches `http://fixtures/...` inside compose. `retries: 1` in CI, `trace: 'retain-on-failure'`.
- **Coverage**: `Microsoft.Testing.Extensions.CodeCoverage` → Cobertura per test project → ReportGenerator (dotnet local tool) merges unit + int, writes `MarkdownSummaryGithub` to `$GITHUB_STEP_SUMMARY` and applies `-minimumCoverageThresholds:lineCoverage=80`. Frontend: Vitest v8 `thresholds: process.env.CI ? { lines: 80 } : undefined`. The gate only runs when `CI` is set; `CI=1 just cov` reproduces it locally.
- **Caching**:
  - NuGet: `packages.lock.json` (`RestorePackagesWithLockFile`), `restore --locked-mode`, `setup-dotnet` cache.
  - npm: `setup-node` `cache: npm`.
  - Docker layers: `docker/bake-action` reading `compose.yaml` with `type=gha` cache.
  - **No Playwright browser cache** (upstream advice: restore ≈ download; `install --with-deps` is needed anyway). Overturns the standing decision.
- **Artifacts**: `if: failure()` only, 7-day retention: Playwright HTML report + traces, TRX/JUnit results. Coverage goes to the job summary, not to artifacts.
- **Hardening**: every Action SHA-pinned (`@<sha> # vX.Y.Z`); workflow-level `permissions: contents: read`; `timeout-minutes` static 10 / tests 15 / e2e 20; runner `ubuntu-24.04`; SDKs from `global.json` / `.nvmrc`.
- **Renovate**: `helpers:pinGitHubActionDigests`; weekly schedule; grouped (e.g. `@types/*`, MSTest); minor/patch/digest automerge once CI is green (no branch protection needed; Renovate checks status itself); majors open PRs for review; `minimumReleaseAge: 3 days`; bumps the runner label.
- **Budget**: the repo is public, so GitHub-hosted minutes are free. Optimise wall-clock time, not spend.
