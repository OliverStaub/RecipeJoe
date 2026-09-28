# Schema.org Recipe variants

Type: research
Status: resolved
Blocked by: —

## Question

How do real recipe sites embed schema.org/Recipe, and which variants must the V1 Import parser handle? Cover: JSON-LD vs microdata/RDFa prevalence, `@graph` wrapping, arrays of types, `recipeInstructions` as strings / `HowToStep` / `HowToSection`, ISO-8601 durations, `recipeYield` shapes, `image` shapes (string / array / `ImageObject`), HTML entities in text. Survey ~10 popular sites (EN + DE). Recommend a .NET parsing approach (System.Text.Json by hand vs a library) and list which pages to save as Import fixtures.

Research: [`research/schema-org-recipe-variants.md`](../../../research/schema-org-recipe-variants.md) (no branch: repo guardrail blocks agent git writes)

## Answer

- JSON-LD only (22 of 23 surveyed sites use it). Microdata is out of scope.
- Parser written by hand on `System.Text.Json` `JsonDocument`, with AngleSharp to pull out the `ld+json` script blocks. Mirror the logic of Python recipe-scrapers' `_schemaorg.py`; don't port per-site scrapers. Rejected: Schema.NET (stale, emit-oriented) and json-ld.net (unmaintained).
- It must handle: object / array / `@graph`; `@type` as string or array; `@id` refs; instructions as string[] / HowToStep / HowToSection (flattened); durations via `XmlConvert.ToTimeSpan` + a regex fallback, where a bad duration never fails the Import; HTML decode (twice if needed), `&nbsp;` → space, strip tags, collapse whitespace.
- Failed Import = no Recipe node, OR no name, OR zero Ingredient Lines and zero Steps.
- Seam: pure `Parse(html, pageUrl) → ParsedRecipe | ImportFailure`, tested from saved HTML fixtures (16 real pages + synthetic ones are listed in the research file).
- Surfaced: bot-blocking by big sites → ticket 11; HowToStep names and recipeYield → ticket 12; image-download failure → ticket 06.
