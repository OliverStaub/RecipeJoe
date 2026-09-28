# 04: E2E package + smoke test

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §3.1 (E2E), §4.3 slice 5

**What to build:** A standalone Playwright package that runs against the compose `app` profile on three device projects.

**Blocked by:** 03 (Compose + container images)

**Status:** ready-for-agent

- [ ] `/e2e` package with its own `package.json`, ESLint + Prettier, `playwright.config.ts`
- [ ] Projects: Chromium, WebKit/iPhone, Pixel; `baseURL` `http://localhost:8080`; `retries: 1`, `trace: 'retain-on-failure'`
- [ ] One smoke test: the Library page shows "Rezepte"; it must not assume an empty Library
- [ ] Green on all 3 projects against a fresh compose `app`
