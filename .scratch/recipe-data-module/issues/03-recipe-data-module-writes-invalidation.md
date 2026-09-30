# 03: Recipe data module: Import/delete writes + invalidation

**Spec:** [../spec.md](../spec.md)

**What to build:** After an Import or a delete, the Library and Cook View always agree without a reload. `useImportRecipe()` and `useDeleteRecipe()` live in the Recipe data module, which alone knows which cached data changes; features never touch the cache.

**Blocked by:** 02 (Recipe data module: reads + "not found" state)

**Status:** resolved

- [x] `useImportRecipe()` invalidates the Library list on success
- [x] `useDeleteRecipe()` removes that Recipe's cached entry and invalidates the Library list; other Recipes' entries untouched
- [x] Import feature's error-kind parsing moves into its failure-message code (no merge of the two; out of scope)
- [x] Import and delete per-feature data files deleted; no feature references cache keys
- [x] Route test: Import → back in the Library, the new Recipe is listed
- [x] Route test: delete from Cook View → back in the Library, the Recipe is no longer listed (extends delete-flow test)
- [x] Hook-level delete test that hand-builds cache keys deleted
- [x] Coverage gate passes
