# Library Import progress UI

Type: prototype
Status: resolved
Blocked by: 03

## Question

How should the Library look and behave with pending, failed and newly imported Recipes? Prototype: pending card (stage label + indeterminate animation), error card with "Erneut versuchen" / "Verwerfen", several concurrent Imports, one video producing N Recipes appearing, and the "new" marker. Standard shadcn components only, German UI.

## Assets

- Prototype: branch `prototype/library-import-progress` (commit 5d5a30e). `cd frontend && npx vite`, open `/?variant=A|B|C`.

## Answer

Variant **A (Inline-Zeilen)** chosen as-is ("Version A ist perfekt").

- Pending and Failed Imports are rows at the top of the Library list, above the Recipes (Library stays newest-first), same row shape as a Recipe. They stay visible while searching.
- Pending row: Skeleton thumbnail (pulse) with kind icon (video/web); title = URL label (host + path, no `www.`); below it a spinner + stage label. No progress bar or percentage.
- Failed row: destructive icon thumbnail, URL label, failure message in destructive text, inline buttons `Erneut versuchen` (outline) / `Verwerfen` (ghost). Retry turns the row back into Pending in place.
- Stage labels: Web `Seite wird geladen…` → `Rezept wird gelesen…` → `Wird gespeichert…`; Video `Video wird geladen…` → `Rezept wird geschrieben…` → `Wird gespeichert…`.
- One video → N Recipes: the row disappears and all N Recipes appear at the top together (matches Library refetch on vanish).
- New marker: shadcn `Badge` "Neu" next to the title. Prototype cleared it when the Recipe was opened; the exact rule is ticketed separately.
- Dialog: closes on submit; only an invalid URL stays inline; placeholder `Webadresse oder YouTube-Link`.
- Components: existing shadcn + `Skeleton` (add via shadcn CLI).
