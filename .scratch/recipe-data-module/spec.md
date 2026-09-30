# Recipe data module (frontend) — spec

**Status:** ready-for-agent

**Origin:** architecture review 2026-09-30, candidate 1. Terms follow [`CONTEXT.md`](../../CONTEXT.md); architecture terms follow `/codebase-design` (module, interface, seam, depth, locality, leverage).

## Problem Statement

The cook opens a Recipe that no longer exists (deleted in another tab, or an old bookmark) and waits several seconds while the app silently retries, then sees "Rezept konnte nicht geladen werden." — as if the server were broken — instead of "Rezept nicht gefunden." Only a non-numeric id in the URL gets the correct message.

For the developer, the knowledge "which cached data must change when a Recipe is imported or deleted" has no home. Each feature (Library, Cook View, Import, delete) owns a one-hook data module whose interface is as wide as its implementation, and Import and delete hard-code cache keys that belong to other features. The tests build their own query client with retries off, so they never see how production behaves; one test rebuilds the query library's internal key shape by hand.

## Solution

One deep frontend Recipe data module owns all Recipe reads and writes: its cache keys, what is invalidated after an Import or a delete, the retry policy, and the meaning of "not found". Features call it and never touch the cache directly. One provider tree is shared by the app and the test render helper, so tests exercise the production query-client configuration.

From the cook's side: a missing Recipe shows "Rezept nicht gefunden." immediately, a transient server hiccup is retried once, and the Library and Cook View always agree after an Import or a delete.

## User Stories

1. As a cook, I want a deleted or unknown Recipe to show "Rezept nicht gefunden." right away, so that I don't wait for something that will never load.
2. As a cook, I want a URL with a non-numeric Recipe id to show "Rezept nicht gefunden.", so that bad links behave like missing Recipes.
3. As a cook, I want a Recipe that fails to load because of a server error to be retried once before I see an error, so that a single blip doesn't interrupt cooking.
4. As a cook, I want a Recipe that still fails after the retry to show "Rezept konnte nicht geladen werden.", so that I can tell a broken server from a missing Recipe.
5. As a cook, I want a Recipe I just imported to appear in the Library when I return to it, so that I don't have to reload the page.
6. As a cook, I want a Recipe I just deleted from the Cook View to be gone from the Library, so that I never open a Recipe that no longer exists.
7. As a cook, I want a Recipe I just deleted from the Library to disappear from the list, so that the list reflects what I have.
8. As a cook, I want other Recipes' cached Cook Views to stay intact after I delete one Recipe, so that reopening them stays instant.
9. As a cook, I want the Library search to keep showing the previous results while a new search loads, so that the list doesn't flicker (existing behaviour, preserved).
10. As a cook, I want a failed Library load to be retried once on a server error and not at all on a client error, so that the error state appears quickly when retrying cannot help.
11. As a developer, I want one module to be the only place that knows Recipe cache keys, so that renaming an endpoint or a key touches one file.
12. As a developer, I want the invalidation rules for Import and delete to live next to the queries they affect, so that adding a new Recipe view means updating one module.
13. As a developer, I want features to call intention-named hooks (Recipes, one Recipe, import, delete), so that they need no knowledge of the query library.
14. As a developer, I want the single-Recipe hook to report "not found" as its own state, so that screens don't decode HTTP status codes.
15. As a developer, I want the app and the test render helper to share one provider tree, so that tests run the production query-client configuration.
16. As a developer, I want each test to get a fresh query client from that shared configuration, so that tests stay isolated.
17. As a developer, I want the retry policy defined once, so that production and tests cannot drift apart again.
18. As a developer, I want cache behaviour tested through screens (route-level tests), so that tests survive refactors of the data module's implementation.
19. As a developer, I want no test to hand-build the query library's key shape, so that upgrading openapi-react-query cannot silently break tests.
20. As a developer, I want the per-feature data files gone, so that there is one obvious place to look for Recipe data access.

## Implementation Decisions

- **New module: frontend Recipe data module**, sitting next to the typed API client. Interface:
  - `useRecipes(q)`: the Library list for a search text; keeps previous data while refetching.
  - `useRecipe(id)`: one Recipe for the Cook View. Returns a state the screen can switch on: loading, not found, error, loaded. "Not found" covers both a non-integer id (no request made) and a 404 response.
  - `useImportRecipe()`: the Import mutation; on success, invalidates the Library list.
  - `useDeleteRecipe()`: the delete mutation; on success, removes that Recipe's cached entry and invalidates the Library list. Other Recipes' entries are untouched.
- Cache keys are derived from the typed client's query-options helpers inside the module, never written out by hand anywhere.
- **Retry policy** (queries): never retry a 4xx; retry any other failure (network, 5xx) once. Mutations are not retried. Defined once, in the shared query-client configuration.
- **Shared provider tree**: one app-providers module creates the query client (from one configuration factory) and renders the query-client provider, router and toaster. The app uses it with the browser router; the test render helper uses it with a memory router and a fresh client per test.
- The four per-feature data files (Library, Cook View, Import, delete) are deleted. The Import feature's error-kind parsing (currently in its data file) moves into the Import feature's failure-message code; merging those two is candidate 4 and out of scope.
- The Cook View switches on the hook's state instead of computing "invalid id" itself and inferring errors from missing data.
- No backend or API contract change.

## Testing Decisions

- Good tests assert what the cook sees through a route (`renderApp` + MSW handlers), not hook internals or cache contents.
- Seam: the existing route-level tests. No new seam.
- Tests to change or add:
  - Cook View: a 404 shows "Rezept nicht gefunden." and is requested exactly once.
  - Cook View: a 500 is requested twice, then shows "Rezept konnte nicht geladen werden."; a 500 that succeeds on retry shows the Recipe.
  - Library: a 4xx shows the error state after one request.
  - Delete from Cook View → back in the Library, the Recipe is no longer listed (extends the existing delete-flow test).
  - Import → back in the Library, the new Recipe is listed.
- The hook-level delete test that rebuilds cache keys by hand is deleted; its intent is covered by the route-level delete tests.
- Prior art: `deleteFlow.test.tsx`, `CookViewPage.test.tsx`, `LibraryPage.test.tsx`, `ImportDialog.test.tsx`, typed MSW handlers in the test server helper.
- Coverage gate in CI must still pass.

## Out of Scope

- Merging the Import error-kind parsing and failure messages into one function (candidate 4).
- A shared Recipe actions menu for delete (candidate 5).
- Building the image URL in one place / aligning the two Recipe DTOs (candidate 6).
- Optimistic updates, prefetching, offline support.
- Any backend change.

## Further Notes

- Retrying 5xx once means one test waits for the retry delay; set a short retry delay in the shared configuration if it slows the suite noticeably, as long as prod and tests use the same value.
- The existing Cook View test "says so when loading the Recipe fails" currently pins the wrong message for a 404; it is expected to change.
