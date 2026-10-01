# 01: Background Web Import via API

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [Background Import mechanism](../../video-import/issues/03-background-import-mechanism.md)

**What to build:** A Web Import can be started through the new Imports API and runs in the background. The caller gets an Import back at once, can watch it go through its stages while Pending, and finds the Recipe in the Library once the Import has disappeared. A Failed Import stays listed with its failure kind until it's retried or dismissed. The old synchronous Import endpoint stays in place for now (expand step), so the frontend keeps working unchanged.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Deep Imports module: Start / List / Retry / Dismiss over an in-memory store (Pending with stage | Failed with kind; removed on success); hosted runner reading ids from an in-process channel; `Import:MaxConcurrent` (default 2); own DI scope per run
- [x] Internal seam `IImportPath` (URL + stage progress → Recipe Drafts | Import failure); the path is chosen by URL and sets the Import kind; the Web path wraps the existing `Importer` unchanged
- [x] Each Recipe Draft is saved in its own transaction; ≥1 saved → Import removed; 0 saved → Failed
- [x] `POST /api/imports {url}` → 202 + ImportDto (id, url, kind, state, stage?, failure?); `InvalidUrl` → 400, no Import created; every other failure (incl. `ForbiddenAddress`) ends up on the Import
- [x] `GET /api/imports` lists Pending and Failed; `POST /api/imports/{id}/retry` → 202, re-runs from scratch with the same id; `DELETE /api/imports/{id}` → 204; unknown id → 404
- [x] Lifecycle unit tests with fake paths: stage progression, concurrency limit, retry, dismiss, partial save, 0 saved → Failed
- [x] API integration tests: Web Import via the new endpoints (poll until gone, then the Recipe is in the Library); each existing failure kind reachable as a Failed Import; `Importer` unit + corpus tests untouched
- [x] OpenAPI document regenerated; coverage gate passes

## Comments

Implemented as planned, with one scope narrowing: URL-based kind classification (YouTube host → Video) isn't wired in yet. Classifying by URL without a registered Video `IImportPath` would route a pasted YouTube link into `ImportRunner` with no keyed service to resolve, crashing the run and surfacing a misleading `SaveFailed`. Every Import is `ImportKind.Web` until ticket 04 adds the Video path and the classifier alongside it. `ImportDto.Kind` still models `Web|Video` per the spec's API shape.

Added `ImportFailure.SaveFailed` (not in the original kind list) for the "Recipe Drafts produced but none could be saved" case, covering the partial-save/0-saved lifecycle rule. The old `POST /api/recipes/import` endpoint is untouched, as scoped.
