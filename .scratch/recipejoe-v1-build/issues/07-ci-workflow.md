# 07: CI workflow + coverage gate

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §3.2, §4.2; decision: [CI workflow design](../../recipejoe-v1/issues/08-ci-workflow-design.md)

**What to build:** One GitHub Actions workflow that runs every quality gate through the same `just` recipes used locally.

**Blocked by:** 05 (justfile)

**Status:** resolved

- [ ] Triggers: push to `main`, `pull_request`, `workflow_dispatch`; `concurrency` with `cancel-in-progress` on PRs; no `paths:` filters
- [ ] Jobs:
  - `static`: `fmt-check`, `lint`, `build`, `test-contract`
  - then `backend-tests` (`cov-backend`) and `frontend-tests` (`cov-frontend`) in parallel
  - then `e2e`: copy `.env.example`, `npx playwright install --with-deps`, `test-e2e`, `down` under `if: always()`
- [ ] Caches: NuGet (lock files, `--locked-mode`), npm, Docker layers via `docker/bake-action` + `type=gha`; no Playwright cache
- [ ] Coverage summary in the job summary; the build fails under 80 % lines (backend and frontend separately)
- [ ] Artifacts only `if: failure()`, kept 7 days: Playwright report + traces, TRX/JUnit
- [ ] Hardening: SHA-pinned Actions with a version comment, `permissions: contents: read`, `timeout-minutes` 10/15/20, `ubuntu-24.04`, SDKs from `global.json`/`.nvmrc`, `extractions/setup-just`
- [x] **Phase 1 exit:** a green run on `main` that exercises every layer
