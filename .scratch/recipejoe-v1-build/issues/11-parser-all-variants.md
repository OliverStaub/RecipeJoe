# 11: Parser covers all schema.org variants

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.5 (Parsing), §2.9; decisions: [Schema.org Recipe variants](../../recipejoe-v1/issues/01-schema-org-recipe-variants.md) (full rules: [research §2](../../../research/schema-org-recipe-variants.md)), [Step titles and Servings parsing](../../recipejoe-v1/issues/12-step-titles-and-servings-parsing.md), [Seed data](../../recipejoe-v1/issues/15-seed-data.md)

**What to build:** Import succeeds for every Recipe page in the fixture corpus and fails with `NoRecipe` for every `failing/` page.

**Blocked by:** 09 (Import a plain Recipe via the API)

**Status:** resolved

- [x] Complete corpus: 30 synthetic pages (~18 plain German, ~12 variants incl. 2–3 English) per §2.9, each with a variant comment
- [x] `failing/`: no-recipe, microdata-only, broken-json-only
- [x] Reads every `ld+json` block (case-insensitive); a malformed block is skipped; trailing commas and comments are tolerated
- [x] Root as object, array or `@graph`; `@type` as string or array, with the schema.org prefix stripped; `WebPage.mainEntity`; `@id` refs; first Recipe wins
- [x] Instructions:
  - a text blob, split on newlines
  - string[]
  - HowToStep: `name` dropped, used only when `text` is missing
  - HowToSection/ItemList, flattened recursively
  - nested arrays
- [x] Text normalisation per §2.5: decode entities (twice if needed), `&nbsp;` → space, `<br>` → `\n`, strip tags, collapse whitespace, trim; Steps keep newlines, 3+ newlines → 2
- [x] Ingredients: fall back to `ingredients`; stringify non-strings
- [x] Durations: `XmlConvert` → upper-case retry → regex; `PT0S` = missing; never fail, never derive the total
- [x] Servings: number, string, longest array element, `QuantitativeValue`; empty → null
- [x] Unit tests: data-driven over the whole corpus, with the expected output per page

## Comments

Done in 253e73f on feature/11-parser-all-variants. Review fixes: cyclic @id guard, overflowing worded durations, empty HowToStep text falls back to name.
