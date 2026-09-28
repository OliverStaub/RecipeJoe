# Frontend structure and seams

Type: grilling
Status: resolved
Blocked by: 02, 09

## Question

What is the folder layout and module seams of `/frontend` for the V1 screens (Library, Import dialog, Cook View): feature folders vs layer folders, where generated-client query hooks and the debounce / `?q=` hook live, how Wake Lock is wrapped so it can be unit-tested, and which behaviour is covered by Vitest vs Playwright? Apply `codebase-design`.

## Comments

- From [Import fetching](11-import-fetching.md): the UI is German only, with strings hard-coded and no i18n library. The English copy in [V1 screens](09-v1-screens.md) (buttons, toasts, delete dialog, headings) must be German. Import failure kinds map to German messages in the frontend.
- From [Step titles and Servings parsing](12-step-titles-and-servings-parsing.md): Steps can contain `\n` → render with `whitespace-pre-line`. The Servings Badge shows the free text as authored, with a lucide `Users` icon.

## Answer

- **Layout** (`frontend/src/`): feature folders `features/library/`, `features/import/`, `features/cook-view/` (route component, hooks, German strings, colocated tests); `api/` (generated `schema.d.ts`, `openapi-fetch` client, `$api`); `components/ui/` (shadcn); `app/` (router, QueryClient, Toaster); `lib/utils.ts` (`cn`).
- **Query hooks**: each feature has an `api.ts` with its hooks (`useRecipes(q)`, `useRecipe(id)`, `useImportRecipe()`, `useDeleteRecipe()`). They own cache invalidation: Import/Delete invalidate the Library query, and Delete also removes the cached Recipe. Components never import `$api`.
- **Search**: one deep `useLibrarySearch()` → `{ input, setInput, query }`. It debounces inline (250 ms, no library), trims, mirrors to `?q=` with `replace`, and seeds from `?q=` on load.
- **Wake Lock**: `useWakeLock()` reads `navigator.wakeLock` directly (no injected seam) and returns `'active' | 'released' | 'unsupported'` for the badge. It acquires on mount, re-acquires on `visibilitychange` → visible, and releases on unmount. Tests stub the global with `vi.stubGlobal`.
- **Network fake in Vitest**: MSW at the `fetch` level, handlers typed from `schema.d.ts`. The real client, hooks and cache run in tests.
- **Vitest** (counts toward coverage): `useLibrarySearch`, `useWakeLock`, the German `ImportFailure` message mapping (exhaustive `switch`), and component tests for each screen's states (Library empty / no hits / list; Import loading / error / error cleared on edit / success navigation; Cook View with optional parts missing; delete flow).
- **Playwright** (3 projects): happy paths against compose + the fixtures site: Import → Cook View (Wake Lock badge asserted only where supported), search narrows the Library, delete from the Library and from the Cook View, plus one `NoRecipeFound` Import to check the error end to end.
- **Locations**: Vitest `*.test.ts(x)` next to the code, MSW setup in `src/test/`. Playwright in its own root `/e2e` package (own `package.json` and `playwright.config.ts`).
- Follows from compose topology (apps on host in dev): the Vite dev server proxies `/api` to the backend, mirroring nginx in the `app` profile.
