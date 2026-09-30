# 02: Recipe data module: reads + "not found" state

**Spec:** [../spec.md](../spec.md)

**What to build:** A cook opening a deleted or unknown Recipe (404 or non-numeric id) sees "Rezept nicht gefunden." right away. Recipe reads live in one Recipe data module next to the typed API client: `useRecipes(q)` for the Library, `useRecipe(id)` for the Cook View, returning loading / not found / error / loaded.

**Blocked by:** 01 (Shared provider tree + retry policy)

**Status:** resolved

- [x] `useRecipes(q)` keeps previous results while a new search loads (behaviour preserved)
- [x] `useRecipe(id)`: non-integer id → not found, no request; 404 → not found
- [x] Cache keys derived from the typed client's query-options helpers inside the module, never hand-written
- [x] Cook View switches on the hook's state; no own "invalid id" computation or error inference from missing data
- [x] Cook View test: a 404 shows "Rezept nicht gefunden." and is requested exactly once (replaces the test pinning the wrong message)
- [x] Library and Cook View per-feature data files deleted
