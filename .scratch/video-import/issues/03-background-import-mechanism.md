# Background Import mechanism

Type: grilling
Status: resolved
Blocked by: —

## Question

How do Imports (both kinds) run in the background? Decide: the domain term for a pending Import, its states and stages; persisted (DB table) vs in-memory, and what happens on backend restart; the runner (hosted service + channel/queue, concurrency limit); API shape (start → 202 + id, then poll vs SSE); how Web Import moves onto it without breaking its existing tests/E2E; retry and dismiss semantics; how the N resulting Recipes link back to their Import. Apply `codebase-design` (deep module, where the seam sits).

## Answer

- **Term**: Import (noun) is Pending (with a stage) or Failed; on success it disappears. No Completed state, no history. Recorded in `CONTEXT.md`. Avoid "Import Job".
- **Persistence**: in-memory only — singleton `ConcurrentDictionary<Guid, Import>` inside the Imports module. Restart loses Pending and Failed Imports (accepted: worst case user re-imports). Frontend treats a vanished Import like a finished one.
- **Runner**: `BackgroundService` reading Import ids from an in-process `Channel`; concurrency `Import:MaxConcurrent` (default 2); own DI scope per run.
- **API**: `POST /api/imports {url}` → 202 + ImportDto · `GET /api/imports` → all Pending/Failed · `POST /api/imports/{id}/retry` → 202 · `DELETE /api/imports/{id}` → 204 (dismiss Failed). Only `InvalidUrl` is synchronous (400, shown inline in dialog, no Import created); everything else (incl. `ForbiddenAddress`) surfaces on the card. Dialog closes on 202.
- **Updates**: polling — TanStack Query `refetchInterval` ~1.5 s while any Import is Pending, off otherwise; an Import disappearing invalidates the Library query. SSE deferred.
- **Seam**: deep Imports module (Start / List / Retry / Dismiss + runner + store). Internal seam `IImportPath.RunAsync(Uri, IProgress<ImportStage>, ct) → Result<IReadOnlyList<RecipeDraft>, ImportFailure>`, path chosen by URL. Web path = existing `Importer` unchanged, wrapping its one draft. Tests drive the lifecycle with fake paths.
- **Saving**: each Recipe saved in its own transaction; Import removed after the last save. ≥1 Recipe saved → success (Import disappears); 0 saved → Failed. Skipped/malformed Recipes belong to LLM output validation (fog).
- **Linking**: none — Recipes don't reference their Import; they carry their Source.
- **Retry/dismiss**: retry re-runs the same Import from scratch (same id, back to Pending). Dismiss deletes a Failed Import. Which failure kinds offer retry → Video Import failure kinds ticket. Cancelling a Pending Import is out of scope.
- **Web Import migration**: remove `POST /api/recipes/import`, regenerate OpenAPI client. `Importer` unit + corpus tests untouched; API integration tests move to the new endpoints (poll until gone); E2E becomes import → card → Recipe in Library (no auto-open of Cook View; rewrite the fixme test).
