# RecipeJoe V1 — wayfinder map

Label: wayfinder:map

## Destination

A resolved architecture + tooling + V1-feature spec (`spec.md`) for RecipeJoe, covering (1) dev tooling / testing / CI and (2) the V1 MVP — ready to generate implementation tickets from (tooling first, then MVP).

## Notes

- Domain: glossary in root `CONTEXT.md` (Recipe, Ingredient Line, Step, Servings, Source, Import, Library, Cook View). Use its terms.
- Skills: grilling + domain-modeling for grilling tickets; `codebase-design` for layout/seams; `research` for research tickets; `prototype` for UI.
- Driving motivation: user wants a very strong testing pipeline and to learn DevOps practices — treat CI/testing as first-class. Explain mechanics; keep CI runner cost in mind.
- Keep it super simple: standard shadcn components only.

### Standing decisions (settled while charting)

- Monorepo: `/backend` (.NET, EF Core, Postgres), `/frontend` (React + TS SPA), root `docker-compose.yml`. Runs **locally only**; no deploy, no published artifacts.
- Single user, no auth (V1).
- Import: schema.org/Recipe JSON-LD only. Failed Import → inline error, nothing saved. Duplicates not detected. Sections flattened into Steps; multi-recipe page → first Recipe.
- V1 features: Import, Library (text search over titles + Ingredient Lines, no filters), Cook View (Wake Lock on, recipe image shown), delete Recipe. No edit, no servings scaling.
- Recipe images are downloaded (not hotlinked).
- Backend: constructor DI via built-in container everywhere; Minimal APIs; EF Core code-first migrations (auto-apply on startup locally; bundles if ever production); CI checks `has-pending-model-changes`; fetcher behind an interface.
- Frontend: Vite + React + TS, React Router, TanStack Query, generated typed API client, no global state lib. shadcn + Tailwind utilities for layout only; no custom `.css` beyond shadcn globals; default theme.
- Test layers (each its own project): unit (MSTest + NSubstitute / Vitest + Testing Library), integration (MSTest + Testcontainers.PostgreSql + WebApplicationFactory, real migrations), contract (OpenAPI → TS client, drift breaks build), E2E (Playwright TS against compose; Chromium, WebKit/iPhone, Pixel), Import fixtures (saved HTML, no network).
- Coverage: unit + integration, line, 80%, backend and frontend separately; excludes migrations, generated client, shadcn `components/ui`; E2E excluded. Enforced **in CI only**; report in job summary.
- Warnings are errors everywhere (`TreatWarningsAsErrors`, nullable, TS strict, ESLint `--max-warnings=0`); suppressions per-line with reason.
- Formatting: `dotnet format` + `.editorconfig`, Prettier + ESLint. Autofix in Claude PostToolUse hook (per file) and lefthook pre-commit (format + lint staged, re-stage). No tests in pre-commit, no pre-push hook. CI is check-only.
- `just` task runner: `test`, `test-unit`, `test-int`, `test-contract`, `test-e2e`, `cov`, `fmt`, `lint`, `up`, `down`, `mutate`. CI calls the same recipes.
- CI (GitHub Actions): lint/format → build → unit → integration → coverage gate → E2E on compose. Caching (NuGet, npm, Playwright). Renovate for deps. Trunk-based, no branch protection.
- Mutation testing (Stryker.NET + StrykerJS): local only via `just mutate`.

## Decisions so far

- [Backend solution layout](issues/04-backend-solution-layout.md): single `RecipeJoe.Api` project with feature folders; `IPageFetcher` is the only seam; deep `Importer`; child tables for lines/steps; Unit/Integration/Contract test projects.

- [OpenAPI + TS client generator](issues/02-openapi-ts-client-generator.md): built-in .NET OpenAPI at build time → openapi-typescript + openapi-fetch + openapi-react-query; CI regenerates and diffs.
- [Schema.org Recipe variants](issues/01-schema-org-recipe-variants.md): JSON-LD only; parser written by hand on System.Text.Json + AngleSharp as a pure, fixture-tested seam; failed-Import criteria defined.
- [Hook, lefthook and just mechanics](issues/03-hook-lefthook-just-mechanics.md): how each layer works, with measured formatter speeds and config sketches; the open choices went to Local automation choices.
- [docker-compose topology](issues/05-docker-compose-topology.md): compose runs only Postgres in dev (apps on host); `profile: app` adds backend, nginx web (SPA + `/api` proxy) and a fixtures site for E2E; fresh DB per E2E run; loopback ports; `.env` (gitignored) + `.env.example`; just recipes per environment.
- [Recipe image storage](issues/06-recipe-image-storage.md): `bytea` in a 1:1 `RecipeImages` table; first candidate only, ≤5 MB, jpeg/png/webp/gif by magic bytes, no resizing; failed download → save without image; served at `GET /api/recipes/{id}/image`, immutable caching.
- [Search implementation](issues/07-search-implementation.md): server-side `ILIKE` over the title + Ingredient Lines, whitespace tokens ANDed, case-insensitive, newest first, summary DTO, 250 ms debounce with `?q=` in the URL.
- [V1 screens](issues/09-v1-screens.md): prototype variant A. List Library with sticky search and ⋮ Delete, Import in a Dialog (inline error, then opens the Cook View), single-scroll Cook View with Wake Lock always on. No tick-off or step mode.

## Not yet specified

- Seed / demo data for local dev and E2E.

## Out of scope

- YouTube-transcript Import (future effort).
- Auth / multiple users.
- Deployment, self-hosting, staging/prod environments, publishing images/artifacts.
- CodeQL.
- Microdata/RDFa parsing (only 1 of 23 surveyed sites uses it; see [Schema.org Recipe variants](issues/01-schema-org-recipe-variants.md)).
- Mutation testing in CI (runner cost).
- Editing Recipes, servings scaling, Library filters, duplicate detection.
