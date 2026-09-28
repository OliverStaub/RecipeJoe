# docker-compose topology

Type: grilling
Status: resolved
Blocked by: 04

## Question

Which services does `docker-compose.yml` define (postgres, backend, frontend, …), and how do dev and E2E use it? Dev: hot reload (dotnet watch / Vite dev server) in containers vs only Postgres in compose and apps on host. E2E: built images composed in CI runner, overrides file, healthchecks, ports, env/secrets handling, DB reset between E2E runs.

## Answer

- **Dev**: compose runs only Postgres. API (`dotnet watch`) and web (Vite, proxies `/api`) run on host. Recipes: `just dev-db`, `just dev-api`, `just dev-web`. No combined `just dev`.
- **One `compose.yaml` with profiles**: `postgres` always; `backend`, `web`, `fixtures` under `profile: app`. No override files.
  - `web`: nginx serves the built SPA and proxies `/api` → backend (single origin, no CORS; backend stays API-only).
  - `fixtures`: nginx serving saved Import HTML + images.
  - Images built via compose `build:` from multi-stage `backend/Dockerfile`, `frontend/Dockerfile`. CI layer caching → [CI workflow design](08-ci-workflow-design.md).
- **just**: `up` = `--profile app up -d --build --wait`; `down`; `reset` = `down -v`; `test-e2e` = `reset` → `up` → Playwright.
- **E2E DB state**: fresh DB each run (`down -v`, migrations auto-apply on startup). Tests don't assume an empty Library; each creates Recipes with unique titles. No test-only reset endpoint.
- **E2E Import**: real HttpClient fetcher → `fixtures` container (e.g. `http://fixtures/<page>.html`). Requires an SSRF allowlist config → [Import fetching](11-import-fetching.md).
- **Health**: Postgres `pg_isready`; backend `/health` (liveness + DB); `up --wait` blocks until healthy.
- **Ports**: all bound to `127.0.0.1`: Postgres 5432, backend 5080, web 8080 (overridable via `.env`).
- **Env**: `.env` (gitignored) + committed `.env.example` with working defaults. Keys: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT`, `API_PORT`, `WEB_PORT`. Compose auto-reads `.env`. `just setup` and CI copy `.env.example` → `.env` if missing (no GitHub secrets). Justfile `set dotenv-load`; `just dev-api` exports `ConnectionStrings__Db` built from those values. Integration tests unaffected (Testcontainers supplies its own).
- **Images**: no volume; [Recipe image storage](06-recipe-image-storage.md) chose Postgres `bytea`.
