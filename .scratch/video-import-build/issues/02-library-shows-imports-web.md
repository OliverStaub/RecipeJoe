# 02: Library shows Imports (Web)

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decisions: [Library Import progress UI](../../video-import/issues/05-library-import-progress-ui.md), [Background Import mechanism](../../video-import/issues/03-background-import-mechanism.md)

**What to build:** The cook submits a URL and the dialog closes right away. The Import shows as a row at the top of the Library, with a spinner and the German stage label. When it ends, the row disappears and the Recipe appears at the top. A failure turns the row into an error row with the existing Web wording and "Erneut versuchen" / "Verwerfen". The old synchronous endpoint is removed (contract step). This is variant A from the prototype (branch `prototype/library-import-progress`).

**Blocked by:** 01

**Status:** resolved

- [x] The Recipe data module gains the Import hooks (list, start, retry, dismiss); features never touch cache keys
- [x] Imports polled ~1.5 s only while any Import is Pending; no requests when idle; an Import vanishing invalidates the Library
- [x] Dialog closes on 202; only `InvalidUrl` stays inline; Cook View no longer opens after an Import
- [x] Pending row: Skeleton thumbnail (added via the shadcn CLI) with the web icon, URL label (host + path, no `www.`), spinner + "Seite wird geladen…" / "Rezept wird gelesen…" / "Wird gespeichert…"
- [x] Failed row: destructive icon, existing Web failure message, "Erneut versuchen" (outline) / "Verwerfen" (ghost); retry turns the row back into Pending in place
- [x] Rows sit above the Recipes and stay visible while searching; several concurrent Imports each get a row
- [x] `POST /api/recipes/import` removed; OpenAPI client regenerated; the old import hook is gone
- [x] Route-level tests (MSW) for the above; API tests no longer use the removed endpoint
- [x] E2E rewritten: Import → row → Recipe in Library (replaces the fixme test)
- [x] Coverage gate passes

## Comments

Backend: removed `POST /api/recipes/import` and its DTOs; `ImportEndpoints.cs` → `ImportServices.cs` (DI registration only, the Web Import building blocks consumed by `WebImportPath`). `DeleteRecipeTests`/`RecipeImageTests` moved their setup onto the async Imports API via a shared `ImportsTestHelper`.

Frontend: `useImports` (poll + vanish→invalidate-Library), `useStartImport`/`useRetryImport`/`useDismissImport` in the Recipe data module. `ImportRow` renders Pending (Skeleton + web/video icon + stage label) and Failed (destructive icon + message + retry/dismiss) rows above the Recipe list in `LibraryPage`; `ImportDialog` now closes on 202 without navigating. Retryability (`isRetryable`) is a frontend map per the spec's own decision, not derived from the API.

E2E: `import.spec.ts` rewritten (replaces the Cook-View-auto-open fixme, which the spec explicitly drops). Since all 3 browser projects run concurrently against one shared backend/DB, Import rows for the same fixture path are visually identical (the label drops the query string by design) — added a `title` attribute carrying the full URL so a test can find its own row, and kept the Recipe-matching helper on exact Source-URL equality (an id-diff alternative broke under real concurrent imports from sibling tests; reverted after confirming against a live run).

Code review (medium) caught two real bugs before commit, both with regression tests verified red→green: dismissing a Failed Import was invalidating the Library query (only a *succeeded* Import should); and the "empty library" invite message could flash before the Imports list's first load resolved.
