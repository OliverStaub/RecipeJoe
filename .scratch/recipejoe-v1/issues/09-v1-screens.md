# V1 screens

Type: prototype
Status: resolved
Blocked by: 01

## Question

What do the V1 screens look like and how do they behave on a phone, using only standard shadcn components: Library (search, list/cards, delete), Import flow (where it lives, loading, inline error), Cook View (image, Servings, times, Ingredient Lines, Steps, Wake Lock). Rough clickable prototype to react to.

## Comments

- From [Search implementation](07-search-implementation.md): the Library search box is search-as-you-type (250 ms debounce), the query is kept in `?q=`, and the list items are summaries (`title`, image if any).
- Prototype (throwaway): `prototype-v1-screens/` — `npm --prefix prototype-v1-screens run dev`, 3 variants via `?variant=A|B|C`. To be moved to branch `prototype/v1-screens` once a variant wins.

## Answer

Variant A of the prototype (`prototype-v1-screens/src/prototype/VariantA.tsx`, captured on branch `prototype/v1-screens`). B and C were rejected.

- **Routes**: `/` Library, `/recipes/:id` Cook View. No separate Import route.
- **Library**: header with the title "Library" and an `Import` button. Below it, a sticky search `Input` (search icon, searches titles + Ingredient Lines). Each row in the list has a 56 px thumbnail (a `ChefHat` placeholder when there's no image), the title, and the Source host, plus a ⋮ `DropdownMenu` → Delete. Empty Library → "Import your first Recipe" link. No hits → "No Recipes match "q"".
- **Import**: a `Dialog` with a URL `Input` and an `Import` button. While loading: the input and button are disabled, the button shows a spinner and "Importing…", and the dialog can't be closed. On failure: a destructive `Alert` inside the dialog (title + detail) that clears when the URL is edited. On success: the dialog closes, a toast says "Recipe imported", and the app goes to that Recipe's Cook View.
- **Cook View**: a sticky top bar (back, truncated title, ⋮ → Open Source / Delete), then the image at 4:3 via `AspectRatio` (skipped if there's none), the title, and `Badge`s for Servings / Prep / Cook / Total (each only if present) plus a Wake Lock status badge. Then Ingredients (plain list), then Steps (an `ol` with numbered circles), then a "From <host>" link at the bottom. Everything on one scroll. No tick-off and no step mode.
- **Wake Lock**: always on while the Cook View is mounted, re-acquired on `visibilitychange`, no toggle. It needs a secure context (https or localhost).
- **Delete**: an `AlertDialog` confirmation ("Delete "title"? This can't be undone."), then a toast "Recipe deleted". From the Cook View it then goes back to the Library.
- **shadcn components used**: button, input, dialog, alert, alert-dialog, dropdown-menu, badge, separator, aspect-ratio, sonner. Current shadcn init defaults to the `base-nova` style (Base UI primitives, `render` prop rather than `asChild`).
- Surfaced: the Import error categories the UI shows (→ Import fetching), Wake Lock needs a secure context (→ CI workflow design), and the frontend folder structure fog graduates into its own ticket (→ Frontend structure and seams).
