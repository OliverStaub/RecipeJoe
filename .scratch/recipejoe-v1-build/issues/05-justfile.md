# 05: justfile

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §4.1 (recipe table); decision: [Hook, lefthook and just mechanics](../../recipejoe-v1/issues/03-hook-lefthook-just-mechanics.md)

**What to build:** One `justfile` that is the single entry point for every local and CI task.

**Blocked by:** 04 (E2E package + smoke test)

**Status:** resolved

- [x] `set positional-arguments`, `set dotenv-load`, `*args` + `"$@"` pass-through
- [x] All recipes from §4.1: `setup`, `dev-db`/`dev-api`/`dev-web`, `seed` (stub), `up`/`down`/`reset`, `build`, `fmt`/`fmt-check`, `lint`, `test`, `test-unit`, `test-int`, `test-contract`, `test-e2e`, `cov`/`cov-backend`/`cov-frontend`, `mutate`
- [x] `dev-api` exports `ConnectionStrings__Db` from `.env`; `setup` copies `.env.example` → `.env` if missing
- [x] `cov-backend`: Cobertura → ReportGenerator (dotnet local tool) merge, `MarkdownSummaryGithub` to `$GITHUB_STEP_SUMMARY` when set, 80 % line gate only when `CI` is set; `CI=1 just cov` reproduces the gate
- [x] `mutate` runs Stryker.NET + StrykerJS
- [x] `just test` green from a clean clone after `just setup`
