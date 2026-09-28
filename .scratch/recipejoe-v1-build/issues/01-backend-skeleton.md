# 01: Backend skeleton + repo baseline

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.1, §2.2, §3.1, §4.3 slices 1–2

**What to build:** A .NET 10 backend that builds and passes its tests, with a `/health` endpoint backed by Postgres, plus the repo-wide baseline config.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Repo baseline: `.gitignore`, `.editorconfig`, `.prettierrc`, `.nvmrc`, `global.json` (.NET 10 SDK), `.env.example` with the §2.8 variables
- [ ] `RecipeJoe.slnx`, `Directory.Build.props` (TreatWarningsAsErrors, nullable, analyzers, coverage exclusions), `Directory.Packages.props`, lock files with `RestorePackagesWithLockFile`
- [ ] `RecipeJoe.Api`: Minimal API host, `GET /health` (liveness + DB check), `DbContext`, initial migration applied on startup, `TimeProvider` registered
- [ ] `openapi.json` written at build time (OpenAPI 3.1); JSON `NumberHandling = Strict`
- [ ] `RecipeJoe.UnitTests` (MSTest + NSubstitute, one trivial test)
- [ ] `RecipeJoe.IntegrationTests` (Testcontainers.PostgreSql + `WebApplicationFactory`, one container per assembly, Respawn; `/health` test)
- [ ] `RecipeJoe.ContractTests` (`openapi.json` drift + `has-pending-model-changes`)
- [ ] `dotnet build` warning-free and `dotnet test` green
