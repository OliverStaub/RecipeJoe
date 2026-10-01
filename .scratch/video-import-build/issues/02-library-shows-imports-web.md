# 02: Library shows Imports (Web)

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decisions: [Library Import progress UI](../../video-import/issues/05-library-import-progress-ui.md), [Background Import mechanism](../../video-import/issues/03-background-import-mechanism.md)

**What to build:** The cook submits a URL and the dialog closes right away. The Import shows as a row at the top of the Library, with a spinner and the German stage label. When it ends, the row disappears and the Recipe appears at the top. A failure turns the row into an error row with the existing Web wording and "Erneut versuchen" / "Verwerfen". The old synchronous endpoint is removed (contract step). This is variant A from the prototype (branch `prototype/library-import-progress`).

**Blocked by:** 01

**Status:** ready-for-agent

- [ ] The Recipe data module gains the Import hooks (list, start, retry, dismiss); features never touch cache keys
- [ ] Imports polled ~1.5 s only while any Import is Pending; no requests when idle; an Import vanishing invalidates the Library
- [ ] Dialog closes on 202; only `InvalidUrl` stays inline; Cook View no longer opens after an Import
- [ ] Pending row: Skeleton thumbnail (added via the shadcn CLI) with the web icon, URL label (host + path, no `www.`), spinner + "Seite wird geladen…" / "Rezept wird gelesen…" / "Wird gespeichert…"
- [ ] Failed row: destructive icon, existing Web failure message, "Erneut versuchen" (outline) / "Verwerfen" (ghost); retry turns the row back into Pending in place
- [ ] Rows sit above the Recipes and stay visible while searching; several concurrent Imports each get a row
- [ ] `POST /api/recipes/import` removed; OpenAPI client regenerated; the old import hook is gone
- [ ] Route-level tests (MSW) for the above; API tests no longer use the removed endpoint
- [ ] E2E rewritten: Import → row → Recipe in Library (replaces the fixme test)
- [ ] Coverage gate passes
