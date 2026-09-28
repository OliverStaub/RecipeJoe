# RecipeJoe V1 — spec

Consolidated from the [wayfinder map](map.md) and its resolved tickets. Each section links the ticket that holds the full reasoning. Terms follow the glossary in [`CONTEXT.md`](../../CONTEXT.md).

Two phases: **Phase 1 Tooling** (a walking skeleton with the full test pipeline, local automation and CI) and **Phase 2 MVP** (the V1 features). Generate implementation tickets in that order, from the slice lists in §4 and §5.

## 1. Scope

**In V1**: Import a Recipe from a URL (schema.org/Recipe JSON-LD), Library (list + text search), Cook View, delete a Recipe. Single user, no auth, runs **locally only**. UI is **German only**; code, API and glossary stay English.

**Out**: YouTube Import, auth/multi-user, deployment/publishing, CodeQL, microdata/RDFa, mutation testing in CI, editing, servings scaling, Library filters, duplicate detection, headless fetching / paste-HTML fallback, i18n.

## 2. Architecture

### 2.1 Repo layout

```
/backend      .NET 10 (RecipeJoe.slnx, src/, tests/)
/frontend     Vite + React + TS SPA
/e2e          Playwright package (own package.json + playwright.config.ts)
/fixtures     recipes/ (30 synthetic pages + images), recipes/failing/, e2e/ (token templates)
compose.yaml  justfile  lefthook.yml  .prettierrc  .editorconfig  .nvmrc  .env.example
.github/workflows/ci.yml  renovate.json  .claude/settings.json (+ hook script)
```

Compose file is `compose.yaml` (the map's older "docker-compose.yml" means this file).

### 2.2 Backend — [Backend solution layout](issues/04-backend-solution-layout.md)

- One project `src/RecipeJoe.Api`: host, Minimal API endpoints, domain, EF Core, adapters. Feature folders `Import/`, `Recipes/`, `Images/`. `internal` by default; `InternalsVisibleTo` UnitTests only.
- DI: built-in container, constructor injection. Per-feature `AddImport()/AddRecipes()/AddImages()` + `MapImportEndpoints()` etc. from `Program.cs`.
- **Only seam**: `IPageFetcher` (URL → content bytes + `Content-Type` | `ImportFailure`), for pages *and* images. Adapters: HttpClient (prod), fixture (tests; serves files from `/fixtures`).
- **Parser**: pure `Parse(string html, Uri pageUrl) → ParsedRecipe | ImportFailure`, no interface.
- **Importer** (deep): `ImportAsync(url) → Result<Recipe, ImportFailure>` does fetch → parse → image download → save in one transaction. Nothing is persisted on failure. The endpoint stays thin.
- Persistence: `DbContext` directly (no repository). `CreatedAt` from an injected `TimeProvider`.
- EF Core code-first migrations, applied automatically on startup. CI checks `has-pending-model-changes`.
- JSON `NumberHandling = Strict` (else ints become `number | string` in TS) — [OpenAPI + TS client generator](issues/02-openapi-ts-client-generator.md).
- Build plumbing: `Directory.Build.props` (TreatWarningsAsErrors, nullable, analyzers, coverage exclusions), `Directory.Packages.props` (central versions), `global.json` (.NET 10 SDK), `packages.lock.json` + `RestorePackagesWithLockFile`.

### 2.3 Data model

| Table | Columns |
|---|---|
| `Recipes` | `Id`, `Title`, `Servings` text null, `PrepTime`/`CookTime`/`TotalTime` interval null, `SourceUrl`, `CreatedAt` |
| `IngredientLines` (EF `OwnsMany`) | `RecipeId`, `Position` int, `Text` |
| `Steps` (EF `OwnsMany`) | `RecipeId`, `Position` int, `Text` (may contain `\n`) |
| `RecipeImages` (1:1) | `RecipeId` PK/FK, `ContentType`, `Bytes` bytea |

All children cascade-delete with the Recipe. Lines and Steps are always loaded with the Recipe; the image is not.

### 2.4 API (OpenAPI 3.1, contract-checked)

| Endpoint | Result |
|---|---|
| `POST /api/recipes/import` `{ url }` | `201` + Recipe DTO + `Location`; `400` (`InvalidUrl`) / `422` (all other kinds): ProblemDetails with `kind: ImportFailure` |
| `GET /api/recipes?q=` | `200` `RecipeSummary[]` = `{ id, title, sourceUrl, hasImage }`, newest first; empty/missing `q` = whole Library |
| `GET /api/recipes/{id}` | `200` Recipe DTO = `{ id, title, servings?, prepMinutes?, cookMinutes?, totalMinutes?, ingredientLines: string[], steps: string[], sourceUrl, imageUrl?, createdAt }`; `404` |
| `DELETE /api/recipes/{id}` | `204`; `404` |
| `GET /api/recipes/{id}/image` | stored bytes + stored content type, `Cache-Control: public, max-age=31536000, immutable`; `404` if none |
| `GET /health` | liveness + DB check (compose healthcheck; not proxied) |

`imageUrl` is set by the backend (`/api/recipes/{id}/image` or null). `ImportFailure` is an enum in the contract.

### 2.5 Import

**Fetching** — [Import fetching](issues/11-import-fetching.md)
- Browser-like headers: Chrome UA, `Accept`, `Accept-Language: de-DE,de;q=0.9,en;q=0.8`, gzip/br (auto-decompress).
- Page and image: 10 s total timeout, 5 MB streamed cap. Redirects by hand (`AllowAutoRedirect=false`), max 5, http/https only. No retries.
- Page must be `text/html` or `application/xhtml+xml`. Charset from header, else meta tag (AngleSharp).
- **SSRF guard** in `SocketsHttpHandler.ConnectCallback` on the connected IP (covers redirects, images, DNS rebinding). Pure `IsPublic(IPAddress)` rejects loopback, RFC1918, link-local (incl. 169.254.169.254), CGNAT, multicast, unspecified, IPv6 ULA/link-local. `Import:AllowedHosts` bypass: `fixtures` in E2E compose, `localhost` in `appsettings.Development.json`, loopback in integration tests.

**Failure kinds** (API returns the kind only; frontend maps it to German with an exhaustive `switch`):

| Kind | Trigger | UI message |
|---|---|---|
| `InvalidUrl` | not absolute http(s) | „Das ist keine gültige Webadresse." |
| `Unreachable` | DNS/connection failure, timeout | „Die Seite ist nicht erreichbar. Prüfe die Adresse und versuch es nochmal." |
| `Blocked` | 401/402/403/429, or challenge page (`cf-mitigated: challenge` / "Just a moment…") | „Diese Seite blockiert automatische Zugriffe und kann nicht importiert werden." |
| `NotFound` | 404/410 | „Diese Seite existiert nicht." |
| `BadResponse` | other non-2xx, not HTML, too large, too many redirects | „Die Seite hat etwas geliefert, das wir nicht lesen können." |
| `ForbiddenAddress` | SSRF guard rejects | „Diese Adresse ist nicht erlaubt." |
| `NoRecipe` | parser failure | „Auf dieser Seite wurde kein Rezept gefunden." |

**Parsing** — [Schema.org Recipe variants](issues/01-schema-org-recipe-variants.md) (full rules: [research §2](../../research/schema-org-recipe-variants.md)), [Step titles and Servings parsing](issues/12-step-titles-and-servings-parsing.md)
- JSON-LD only. AngleSharp pulls every `script[type=application/ld+json]` (case-insensitive); `System.Text.Json` `JsonDocument` by hand (trailing commas, comments skipped; a malformed block is skipped, not fatal). Mirror recipe-scrapers' `_schemaorg.py`.
- Root may be object / array / `@graph`; `@type` string or array (strip `http(s)://schema.org/`); `WebPage.mainEntity`; `@id` refs resolved via a map. First Recipe node wins.
- Instructions: string blob (split on newlines) / string[] / HowToStep / HowToSection + ItemList (flattened recursively) / nested arrays. HowToStep `name` is **dropped**; used only if `text` is missing. No Step titles.
- Text normalisation (name, Lines, Steps): HTML-decode (twice if entities remain) → `&nbsp;`→space → `<br>`→`\n`, strip tags → collapse whitespace → trim. In Steps: `\r\n`→`\n`, 3+ newlines → 2, keep newlines.
- Ingredient Lines: `recipeIngredient` (fallback `ingredients`), stringify non-strings.
- Durations: `XmlConvert.ToTimeSpan`, retry upper-cased, then regex (h/hour/Std/Stunde/min/Minute…), else missing. `PT0S` = missing. Never derive totalTime. Never fail on a bad duration.
- Servings: free text. Number → string; string → as-is; array → longest element; `QuantitativeValue` → `value` (+ ` unitText`); empty → null.
- Image: resolve `@id` → first array element → `url ?? contentUrl` → resolve relative to page URL.
- **Failed Import** (`NoRecipe`): no Recipe node, or no name, or zero Lines and zero Steps.

**Images** — [Recipe image storage](issues/06-recipe-image-storage.md)
- First candidate only, via `IPageFetcher`. ≤ 5 MB; jpeg/png/webp/gif by magic bytes. Stored as-is (no resize). Any failure → Recipe saved without image, logged, no user warning.

### 2.6 Search — [Search implementation](issues/07-search-implementation.md)

Server-side `ILIKE '%token%'`; `q` split on whitespace, tokens ANDed; each token matches the title OR any Ingredient Line (`EXISTS`). `%`, `_`, `\` escaped. Case-insensitive, not accent-insensitive. No index, no FTS, no pagination. Order `CreatedAt` desc.

### 2.7 Frontend — [Frontend structure and seams](issues/14-frontend-structure-and-seams.md), [V1 screens](issues/09-v1-screens.md)

- Stack: Vite + React + TS strict, React Router, TanStack Query, `openapi-typescript` + `openapi-fetch` + `openapi-react-query`. No global state lib. shadcn (`base-nova` style, Base UI, `render` prop) with the default theme; Tailwind utilities for layout only; no custom `.css` beyond shadcn globals. Components: button, input, dialog, alert, alert-dialog, dropdown-menu, badge, separator, aspect-ratio, sonner.
- Layout `frontend/src/`: `features/{library,import,cook-view}/` (route component, hooks, German strings, colocated tests); `api/` (generated `schema.d.ts`, client, `$api`); `components/ui/`; `app/` (router, QueryClient, Toaster); `lib/utils.ts`; `test/` (MSW setup).
- Each feature's `api.ts` owns its hooks (`useRecipes(q)`, `useRecipe(id)`, `useImportRecipe()`, `useDeleteRecipe()`) and cache invalidation (Import/Delete invalidate the Library; Delete also removes the cached Recipe). Components never import `$api`.
- `useLibrarySearch()` → `{ input, setInput, query }`: inline 250 ms debounce, trim, mirror to `?q=` with `replace`, seed from `?q=`. Query uses `placeholderData: keepPreviousData`.
- `useWakeLock()` reads `navigator.wakeLock` directly → `'active' | 'released' | 'unsupported'`; acquire on mount, re-acquire on `visibilitychange` → visible, release on unmount. Needs a secure context.
- Vite dev server proxies `/api` → backend (mirrors nginx).

**Screens** (phone-first, variant A of the prototype on branch `prototype/v1-screens`)
- Routes: `/` Library, `/recipes/:id` Cook View.
- **Library**: header (title + Import button); sticky search `Input` with icon; rows = 56 px thumbnail (`ChefHat` placeholder) + title + Source host + ⋮ `DropdownMenu` → Delete. Empty state links to Import; no-hits message quotes `q`.
- **Import** `Dialog`: URL `Input` + button. Loading: inputs disabled, spinner, dialog not closable. Failure: destructive `Alert` (title + kind message), cleared when the URL is edited. Success: close, toast, navigate to the Cook View.
- **Cook View**: sticky top bar (back, truncated title, ⋮ → Open Source / Delete); image 4:3 `AspectRatio` (omitted if none); title; `Badge`s for Servings (lucide `Users`, text as authored), Prep, Cook, Total (each only if present) and Wake Lock status; Ingredients (plain list); Steps (`ol`, numbered circles, `whitespace-pre-line`); "From <host>" link. One scroll; no tick-off, no step mode.
- **Delete**: `AlertDialog` confirmation, then toast; from the Cook View go back to the Library.

**German copy** (wording may be tuned during implementation)

| Where | Text |
|---|---|
| Library heading | Rezepte |
| Import button / dialog title | Importieren / Rezept importieren |
| Import loading | Wird importiert… |
| Import error alert title | Import fehlgeschlagen |
| Import success toast | Rezept importiert |
| Search placeholder | Rezepte durchsuchen… |
| Empty Library | Importiere dein erstes Rezept |
| No hits | Keine Rezepte zu „{q}" |
| Delete menu item / confirm button / cancel | Löschen / Löschen / Abbrechen |
| Delete confirm | „{title}" löschen? Das kann nicht rückgängig gemacht werden. |
| Delete toast | Rezept gelöscht |
| Cook View sections | Zutaten / Zubereitung |
| Time badges | Vorbereitung / Kochen / Gesamt, formatted „1 Std. 15 Min.", „20 Min." |
| Wake Lock badge | Bildschirm bleibt an / Bildschirmsperre nicht verfügbar |
| Source | Quelle öffnen (menu) / Von {host} (footer) |

### 2.8 Compose and environments — [docker-compose topology](issues/05-docker-compose-topology.md), [Seed data](issues/15-seed-data.md)

- One `compose.yaml` with profiles, no override files:
  - `postgres` (always; `pg_isready` healthcheck; `127.0.0.1:5432`).
  - `fixtures` (always): nginx, `./fixtures:/usr/share/nginx/html:ro`, `127.0.0.1:8081`; `sub_filter '__TOKEN__' $arg_t` for `e2e/` pages.
  - `profile: app`: `backend` (multi-stage `backend/Dockerfile`, `/health` healthcheck, `127.0.0.1:5080`, `Import__AllowedHosts=fixtures`); `web` (multi-stage `frontend/Dockerfile`: nginx serving the SPA + proxying `/api` → backend, `127.0.0.1:8080`).
- **Dev**: compose runs `postgres` + `fixtures`; API (`dotnet watch`) and web (Vite) on the host.
- **E2E**: fresh DB per run (`down -v`, migrations on startup); tests never assume an empty Library or seed data.
- **Env**: `.env` (gitignored) from committed `.env.example` (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT`, `API_PORT`, `WEB_PORT`). Justfile `set dotenv-load`; `just dev-api` exports `ConnectionStrings__Db`. No GitHub secrets.

### 2.9 Fixtures — [Seed data](issues/15-seed-data.md)

- `/fixtures/recipes/`: 30 synthetic, committed static HTML pages (no generator script, no third-party content), each opening with a comment naming its variant(s). ~18 plain German Recipes; ~12 variant pages (2–3 English): `@graph` + `@id`, top-level array, `@type: ["Recipe"]`, HowToSection(s), string[] instructions, single blob, HowToStep `name`, non-ISO / `P0Y…` / `PT01H15M` durations, numeric / array / "4 to 6" yield, entities + `<br>`, Recipe in a later block + one malformed block, multi-recipe page.
- `/fixtures/recipes/failing/`: no-recipe, microdata-only, broken-json-only (never seeded).
- Images: tiny generated placeholders (mostly JPEG; one PNG, WebP, GIF). One Recipe without image; one with a 404 image URL. Oversize and wrong-type images are generated inside tests.
- `/fixtures/e2e/`: templates with `__TOKEN__` in the title; tests import `http://fixtures/e2e/<page>.html?t=<uuid>`.
- Consumers: UnitTests via csproj link (`../../fixtures/**`, copied to output); fixture `IPageFetcher`; `fixtures` nginx; dev seed.

## 3. Quality gates

### 3.1 Test layers

| Layer | Tooling | Covers |
|---|---|---|
| Backend unit | MSTest + NSubstitute, `RecipeJoe.UnitTests` | parser over the fixture corpus; Importer with the fixture fetcher (incl. image cases: oversize, bad magic bytes, 404, none); `IsPublic` table tests |
| Backend integration | MSTest + Testcontainers.PostgreSql + `WebApplicationFactory`, `RecipeJoe.IntegrationTests` | HTTP only. One container per assembly (`[AssemblyInitialize]`), real migrations once, Respawn between tests, `FakeTimeProvider`, fixture fetcher swapped in. Search cases (title hit, Line hit, split tokens, case, no match, `%`/`_` literal, order); image endpoint (bytes, type, cache header, 404); delete. HttpClient fetcher vs WireMock.Net on loopback (status mapping, redirects, caps, timeouts); loopback *without* allowlist → `ForbiddenAddress` |
| Contract | `RecipeJoe.ContractTests` + TS | build writes `openapi.json` → regenerate `schema.d.ts` → `git diff --exit-code` on both → `tsc --noEmit`; EF `has-pending-model-changes` |
| Frontend unit | Vitest + Testing Library + MSW (typed from `schema.d.ts`) | `useLibrarySearch` (fake timers), `useWakeLock` (`vi.stubGlobal`), failure-message mapping, screen states: Library empty / no hits / list; Import loading / error / cleared on edit / success nav; Cook View with optional parts missing; delete flow |
| E2E | Playwright (`/e2e`), projects Chromium, WebKit/iPhone, Pixel, vs compose `app` at `http://localhost:8080` | Import → Cook View (Wake Lock badge only where supported); search narrows the Library; delete from Library and from Cook View; one `NoRecipe` Import |
| Mutation | Stryker.NET + StrykerJS | local only, `just mutate` |

### 3.2 Coverage

Line coverage ≥ 80 %, unit + integration, backend and frontend separately; E2E excluded. Excludes migrations, generated client (`schema.d.ts`), `components/ui`. Backend: `Microsoft.Testing.Extensions.CodeCoverage` → Cobertura → ReportGenerator (dotnet local tool) merges, writes `MarkdownSummaryGithub` to `$GITHUB_STEP_SUMMARY`, `-minimumCoverageThresholds:lineCoverage=80`. Frontend: Vitest v8 `thresholds: process.env.CI ? { lines: 80 } : undefined`. Gate enforced only when `CI` is set; `CI=1 just cov` reproduces it.

### 3.3 Static checks

Warnings are errors: `TreatWarningsAsErrors`, nullable, TS strict, ESLint `--max-warnings=0`. Suppressions per line with a reason. Formatting: `dotnet format` + `.editorconfig`; Prettier (root `.prettierrc`) for frontend, e2e and root YAML/JSON (not Markdown), called via `frontend/node_modules/.bin/prettier` (no root `package.json`).

## 4. Phase 1 — Tooling

### 4.1 Local automation — [Hook, lefthook and just mechanics](issues/03-hook-lefthook-just-mechanics.md), [Local automation choices](issues/13-local-automation-choices.md)

Config sketches: [research note](../../research/hook-lefthook-just-mechanics.md), adjusted as below.

- **Claude PostToolUse hook** (`.claude/settings.json` + script): read `tool_input.file_path`, find the repo root from it (not `CLAUDE_PROJECT_DIR`). `.cs` → `dotnet format whitespace --folder --include` (~0.8 s). TS/JS → `eslint --fix` + `prettier --write`; unfixable ESLint errors → exit 2 (stderr to Claude). Tool failures → exit 0.
- **lefthook pre-commit** (`min_version: 2.1.0`): per sub-project `root:`, `stage_fixed: true`. Backend: full `dotnet format --include <staged>` (no `--no-restore`). Frontend/e2e: ESLint then Prettier, in sequence. Root YAML/JSON: Prettier. No tests, no pre-push. Install: `brew install lefthook just` + `just setup`.
- **just** (`set positional-arguments`, `set dotenv-load`, `*args` + `"$@"` pass-through). CI calls the same recipes.

| Recipe | Does |
|---|---|
| `setup` | copy `.env.example` → `.env` if missing, `lefthook install`, `npm ci` (frontend, e2e), `dotnet restore`, `dotnet tool restore` |
| `dev-db` / `dev-api` / `dev-web` | compose `postgres` + `fixtures` / `dotnet watch` with `ConnectionStrings__Db` / Vite |
| `seed` | dev seed (§5) |
| `up` / `down` / `reset` | `--profile app up -d --build --wait` / `down` / `down -v` |
| `build` | backend + frontend build |
| `fmt` / `fmt-check` | fix / verify (backend, frontend, e2e, root YAML/JSON) |
| `lint` | ESLint, `tsc --noEmit`, backend build with analyzers |
| `test` | all layers |
| `test-unit` | backend unit + Vitest |
| `test-int` | backend integration (Testcontainers; no compose dependency) |
| `test-contract` | OpenAPI/TS drift + `has-pending-model-changes` |
| `test-e2e` | `reset` → `up` → Playwright in `/e2e` |
| `cov` (`cov-backend`, `cov-frontend`) | tests with coverage; gate when `CI` is set |
| `mutate` | Stryker.NET + StrykerJS |

### 4.2 CI — [CI workflow design](issues/08-ci-workflow-design.md)

- One `.github/workflows/ci.yml`. Triggers: push to `main`, `pull_request`, `workflow_dispatch`; `concurrency` with `cancel-in-progress` on PRs; no `paths:` filters, no nightly.
- Jobs: `static` (`just fmt-check`, `just lint`, `just build`, `just test-contract`) → `backend-tests` (`just cov-backend`) ∥ `frontend-tests` (`just cov-frontend`) → `e2e` (copy `.env.example`, `npx playwright install --with-deps`, `just test-e2e`, `just down` under `if: always()`; Playwright `retries: 1`, `trace: 'retain-on-failure'`).
- No build reuse between jobs. Caches: NuGet (lock files, `--locked-mode`, `setup-dotnet`), npm (`setup-node`), Docker layers (`docker/bake-action` on `compose.yaml`, `type=gha`). No Playwright browser cache.
- Artifacts only `if: failure()`, 7 days: Playwright report + traces, TRX/JUnit.
- Hardening: SHA-pinned Actions (`@<sha> # vX.Y.Z`), `permissions: contents: read`, `timeout-minutes` 10/15/20, `ubuntu-24.04`, SDKs from `global.json` / `.nvmrc`, `extractions/setup-just`.
- **Renovate**: `helpers:pinGitHubActionDigests`, weekly, grouped (`@types/*`, MSTest…), automerge minor/patch/digest when green, majors as PRs, `minimumReleaseAge: 3 days`, bumps runner label. Trunk-based, no branch protection. Public repo → minutes are free; optimise wall-clock.

### 4.3 Slices (suggested ticket order)

1. **Repo baseline**: `.gitignore`, `.editorconfig`, `.prettierrc`, `.nvmrc`, `global.json`, `.env.example`.
2. **Backend skeleton**: slnx, `Directory.*.props`, lock files, `RecipeJoe.Api` with `/health`, `DbContext` + initial migration + auto-migrate, build-time `openapi.json`, `NumberHandling.Strict`; three test projects each with one real test (unit trivial, integration `/health` via Testcontainers, contract drift).
3. **Frontend skeleton**: Vite/React/TS strict, ESLint, Tailwind + shadcn init, Router, QueryClient, generated `api/`, MSW setup, Vitest + coverage config, Vite `/api` proxy, one rendered route.
4. **Compose + images**: `compose.yaml`, Dockerfiles, nginx `web` config, `fixtures` service with `sub_filter`.
5. **E2E package**: `/e2e`, 3 projects, one smoke test against `up`.
6. **justfile**: all recipes in §4.1 (stubs where features don't exist yet).
7. **Local hooks**: lefthook + Claude hook.
8. **CI workflow** incl. coverage summary/gate.
9. **Renovate**.

Exit criterion: a green `ci.yml` run on `main` exercising every layer against the skeleton.

## 5. Phase 2 — MVP

### 5.1 Dev seed

`just seed` (bash + curl + jq), dev only, never automatic: `GET /api/recipes`; if not empty, exit with a message. Else `POST /api/recipes/import` for every `/fixtures/recipes/` page except `failing/`, with `url=http://localhost:8081/recipes/<name>.html`. One ✓/✗ line per page with the kind; non-zero exit on any failure. Fresh start: `just reset && just dev-db && just seed`.

### 5.2 Slices (suggested ticket order)

1. **Fixture corpus**: `/fixtures/recipes/`, `failing/`, `e2e/`, images.
2. **Parser**: pure `Parse` + unit tests over the corpus.
3. **Data model**: entities + migration (§2.3).
4. **HttpClient fetcher**: headers, limits, redirects, SSRF guard, `IsPublic`, WireMock tests.
5. **Importer + `POST /api/recipes/import`**: incl. image download/validation; fixture fetcher tests; integration tests.
6. **Read + delete endpoints**: list/search, get, image, delete; integration tests.
7. **Library screen**: list, search (`useLibrarySearch`), empty/no-hits states.
8. **Import dialog**: states + German failure mapping.
9. **Cook View**: layout, badges, time formatting, `useWakeLock`.
10. **Delete flows**: from Library and Cook View.
11. **`just seed`**.
12. **E2E happy paths** (§3.1).

Slices 1–6 are backend and can run ahead; 7–10 need the contract from 5–6.

## 6. Reconciliations made while assembling (confirmed by the user)

- `ImportFailure`: the 7 kinds from [Import fetching](issues/11-import-fetching.md) replace the 3 in [Backend solution layout](issues/04-backend-solution-layout.md); `NoRecipe` replaces `NoRecipeFound`/`InvalidRecipe`. Body field is `kind`; `InvalidUrl` keeps `400`, the rest `422`.
- Fixtures live at `/fixtures/recipes/` (synthetic), not `backend/tests/Fixtures/` (real pages) — per [Seed data](issues/15-seed-data.md).
- `fixtures` compose service is always on, not under `profile: app`.
- No Playwright browser cache (CI ticket overturned the standing decision).
- Playwright lives in `/e2e`, not `frontend/` as in the research justfile sketch; `test-int` doesn't depend on `up` (Testcontainers).
- `just fmt --check` from the research sketch → separate `fmt-check`.
- `RecipeSummary` gains `sourceUrl`: the Library row shows the Source host.
- `DELETE /api/recipes/{id}` wasn't specified by any ticket; added.
- Times: stored as nullable `interval`, exposed as integer minutes, formatted in German by the frontend.
- `cov` split into `cov-backend` / `cov-frontend`; CI test jobs run coverage once instead of tests + coverage separately.
