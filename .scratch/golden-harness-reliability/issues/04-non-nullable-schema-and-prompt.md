# 04: Non-nullable schema and prompt

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-prompting-and-schema-design.md](../research-prompting-and-schema-design.md)

**What to build:** A reply with titled but empty recipes can no longer be valid output. Ingredient lines and steps are required, non-null lists with at least one entry, and the prompt shows the model both a full reply and the literal empty reply. The video with no Recipe ends as "no recipe found" instead of ten stub entries.

**Blocked by:** 03

**Status:** in-progress (golden verification pending)

- [ ] Ingredient lines and steps are non-nullable lists with a minimum of one entry in the generated schema (schema-level, verified in the request-shape test)
- [ ] Fields carry short descriptions that say what goes in them (plain strings, one per line/step)
- [ ] No `hasRecipe`/`reason` fields: the "no recipe" decision belongs to the pre-call (ticket 05)
- [ ] Prompt: transcript first, task and reply-shape instructions last; empty case shown as a literal `{"recipes": []}` example next to the full example
- [ ] Parser stays strict (no tolerance for stubs or bare arrays)
- [ ] Extractor tests (fake chat client) still map bad replies to `LlmBadOutput`
- [ ] Verify: `just golden`; note per-video result in Comments

## Comments

- Done: `IngredientLines`/`Steps` non-nullable with `[Description]` + `[MinLength(1)]`; user message now ends with the task and the literal `{"recipes": []}` reply; request-shape test asserts non-null array type, description and min-one. Unit tests green (236).
- Deviation: the chat library's strict transform turns `minItems` into a `minItems: 1` line in the description, not a schema keyword. So min-one is a hint to the model, not enforced by the schema. `Validate` stays the backstop.
- Prompt already had the full example and the empty-reply example from ticket 01, so those weren't changed.
- NOT verified: `just golden` could not run in the agent shell (no `Llm__ApiKey`). Run it 3x and record per-video results (expected counts 0, 1, 5) here.
