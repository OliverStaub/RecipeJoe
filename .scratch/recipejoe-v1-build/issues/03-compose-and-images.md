# 03: Compose + container images

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.8; decision: [docker-compose topology](../../recipejoe-v1/issues/05-docker-compose-topology.md)

**What to build:** One `compose.yaml` that always runs Postgres and the fixtures site, and runs the full app (backend + nginx web) under `profile: app`.

**Blocked by:** 01 (Backend skeleton), 02 (Frontend skeleton)

**Status:** resolved

- [ ] `postgres` service with a `pg_isready` healthcheck on `127.0.0.1:5432`, credentials from `.env`
- [ ] `fixtures` nginx service serving the fixtures dir read-only on `127.0.0.1:8081`, with `sub_filter '__TOKEN__' $arg_t` for `e2e/` pages (one placeholder page to prove it)
- [ ] Multi-stage backend Dockerfile; `backend` service with a `/health` healthcheck, `127.0.0.1:5080`, `Import__AllowedHosts=fixtures`
- [ ] Multi-stage frontend Dockerfile: nginx serving the SPA and proxying `/api` → backend; `web` on `127.0.0.1:8080`
- [ ] `docker compose --profile app up -d --build --wait` succeeds; the SPA loads on :8080; `/health` returns 200 on :5080
