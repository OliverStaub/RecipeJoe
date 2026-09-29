# 15: Library list + search

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.4, §2.6, §2.7 (Library); decisions: [Search implementation](../../recipejoe-v1/issues/07-search-implementation.md), [Frontend structure and seams](../../recipejoe-v1/issues/14-frontend-structure-and-seams.md)

**What to build:** The Library lists every Recipe, newest first. Typing in the search narrows it by title and Ingredient Lines.

**Blocked by:** 13 (Recipe images)

**Status:** done

- [x] `GET /api/recipes?q=` → `RecipeSummary[]` `{ id, title, sourceUrl, hasImage }`, ordered by `CreatedAt` desc; an empty or missing `q` returns the whole Library
- [x] Search: whitespace tokens ANDed; each token `ILIKE` on the title OR any Ingredient Line (`EXISTS`); `%`, `_`, `\` escaped
- [x] Integration: title hit, Line hit, split tokens, case, no match, `%`/`_` literal, order
- [x] `useRecipes(q)` with `keepPreviousData`
- [x] `useLibrarySearch()`: 250 ms debounce, trim, mirror to `?q=` with `replace`, seed from `?q=`
- [x] Sticky search `Input` with an icon and "Rezepte durchsuchen…"
- [x] Rows: 56 px thumbnail (`ChefHat` placeholder) + title + Source host
- [x] Empty state "Importiere dein erstes Rezept" with a link to Import; no hits: "Keine Rezepte zu „{q}""
- [x] Vitest: `useLibrarySearch` with fake timers; Library empty, no hits and list
- [x] E2E: search narrows the Library
