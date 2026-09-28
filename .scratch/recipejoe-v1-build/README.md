# RecipeJoe V1 — build tickets

Spec: [../recipejoe-v1/spec.md](../recipejoe-v1/spec.md). It links the decision tickets in `../recipejoe-v1/issues/`.

Implementation tickets are in `issues/`, numbered in dependency order. Phase 1 Tooling is 01–08, Phase 2 MVP is 09–17. Phase 2 starts once CI is green (07).

| # | Ticket | Blocked by |
|---|---|---|
| 01 | Backend skeleton + repo baseline | — |
| 02 | Frontend skeleton | 01 |
| 03 | Compose + container images | 01, 02 |
| 04 | E2E package + smoke test | 03 |
| 05 | justfile | 04 |
| 06 | Local hooks (lefthook + Claude PostToolUse) | 05 |
| 07 | CI workflow + coverage gate | 05 |
| 08 | Renovate | 07 |
| 09 | Import a plain Recipe via the API (tracer bullet) | 07 |
| 10 | Import from the UI → basic Cook View | 09 |
| 11 | Parser covers all schema.org variants | 09 |
| 12 | HttpClient fetcher + SSRF guard | 09 |
| 13 | Recipe images | 10 |
| 14 | Complete Cook View | 10 |
| 15 | Library list + search | 13 |
| 16 | Delete a Recipe | 14, 15 |
| 17 | `just seed` | 11, 12, 13 |
