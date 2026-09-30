# 17: `just seed`

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §5.1; decision: [Seed data](../../recipejoe-v1/issues/15-seed-data.md)

**What to build:** A developer can fill an empty dev Library with the whole fixture corpus using one command.

**Blocked by:** 11 (Parser covers all schema.org variants), 12 (HttpClient fetcher + SSRF guard), 13 (Recipe images)

**Status:** resolved

- [x] `just seed` (bash + curl + jq); dev only, never run automatically
- [x] `GET /api/recipes`; if the Library is not empty, exit with a message
- [x] `POST /api/recipes/import` for every `recipes/` page except `failing/`, with `url=http://localhost:8081/recipes/<name>.html`
- [x] One ✓/✗ line per page, with the kind; non-zero exit on any failure
- [x] With the API running, `just reset && just dev-db && just seed` yields 30 Recipes, with images where expected
