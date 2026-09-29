# 09: Import a plain Recipe via the API (tracer bullet)

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.2–2.5, §2.9; decision: [Backend solution layout](../../recipejoe-v1/issues/04-backend-solution-layout.md)

**What to build:** Posting the URL of a plain German fixture page to `POST /api/recipes/import` creates a Recipe, which `GET /api/recipes/{id}` returns. A failed Import returns the `ImportFailure` kind and saves nothing.

**Blocked by:** 07 (CI workflow + coverage gate)

**Status:** resolved

- [x] A few plain German fixture pages (synthetic, variant comment at the top), linked into the UnitTests output
- [x] `IPageFetcher` seam + fixture adapter serving files from the fixtures dir
- [x] Pure `Parse(html, pageUrl)` for a single Recipe object: name, `recipeIngredient`, string[]/HowToStep instructions, ISO durations, Servings as text; `NoRecipe` when there is no Recipe node, no name, or zero Lines and zero Steps
- [x] Entities + migration:
  - `Recipes`, incl. Servings, Prep/Cook/Total as `interval`, `SourceUrl`, `CreatedAt` from `TimeProvider`
  - `IngredientLines` and `Steps` as `OwnsMany` with `Position`
- [x] Deep `Importer.ImportAsync(url)`: fetch → parse → save in one transaction; nothing persisted on failure
- [x] `POST /api/recipes/import`
  - `201` + Recipe DTO + `Location`
  - `400` for `InvalidUrl`, `422` for the other kinds; ProblemDetails with `kind`
  - the `ImportFailure` enum in the contract holds all 7 kinds
- [x] `GET /api/recipes/{id}`: Recipe DTO (times as int minutes, `imageUrl` null for now); `404`
- [x] Unit tests (parser, Importer with the fixture fetcher) and integration tests over HTTP; contract regenerated
