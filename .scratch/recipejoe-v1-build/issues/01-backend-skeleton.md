# 01: Backend skeleton + repo baseline

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.1, §2.2, §3.1, §4.3 slices 1–2

**What to build:** A .NET 10 backend that builds and passes its tests, with a `/health` endpoint backed by Postgres, plus the repo-wide baseline config.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Repo baseline: `.gitignore`, `.editorconfig`, `.prettierrc`, `.nvmrc`, `global.json` (.NET 10 SDK), `.env.example` with the §2.8 variables
- [x] `RecipeJoe.slnx`, `Directory.Build.props` (TreatWarningsAsErrors, nullable, analyzers, coverage exclusions), `Directory.Packages.props`, lock files with `RestorePackagesWithLockFile`
- [x] `RecipeJoe.Api`: Minimal API host, `GET /health` (liveness + DB check), `DbContext`, initial migration applied on startup, `TimeProvider` registered
- [x] `openapi.json` written at build time (OpenAPI 3.1); JSON `NumberHandling = Strict`
- [x] `RecipeJoe.UnitTests` (MSTest + NSubstitute, one trivial test)
- [x] `RecipeJoe.IntegrationTests` (Testcontainers.PostgreSql + `WebApplicationFactory`, one container per assembly, Respawn; `/health` test)
- [x] `RecipeJoe.ContractTests` (`openapi.json` drift + `has-pending-model-changes`)
- [x] `dotnet build` warning-free and `dotnet test` green

## Comments

Build-time OpenAPI generation (`GetDocument.Insider`) re-executes `Program.cs` up to `app.Run()`, so the startup migration call is guarded behind an `Assembly.GetEntryAssembly()` check (the documented pattern for this scenario) to avoid it trying to connect to a database during `dotnet build`.

The initial migration is empty (no entities yet — those land in the Phase 2 data-model ticket), so it exists only to prove the EF Core + Postgres + migrate-on-startup pipeline works end-to-end.

Respawn is wired into `ApiFactory.ResetDatabaseAsync`, but since the model has no tables yet, `Respawner.CreateAsync` throws until real entities exist; that's caught as a no-op for now rather than skipping the mechanism entirely.

`appsettings.json` (non-Development) has no `ConnectionStrings:Db` by design — per spec §2.8 it's supplied via `ConnectionStrings__Db` env var from the justfile/compose, which land in later tickets.

Reviewed via `/code-review`; one real finding (an overly broad try/catch in `ApiFactory.ResetDatabaseAsync` that could swallow unrelated Respawn failures) was fixed by narrowing the catch to only the "no tables yet" bootstrap case.
