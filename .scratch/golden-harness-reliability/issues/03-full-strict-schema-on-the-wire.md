# 03: Full strict schema on the wire

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-dotnet-sdk-handling.md](../research-dotnet-sdk-handling.md)

**What to build:** The outgoing request carries the complete strict JSON schema that the chat library generates (including `additionalProperties: false`), not a hand-rolled variant without it. Today's `strict` workaround replaces the library's schema and drops that constraint, which strict-mode providers may reject (suspected cause of the fast `error` failures; unconfirmed).

**Blocked by:** None (can start immediately)

**Status:** done (golden re-run pending: needs Llm__ApiKey)

- [x] `strict: true` is requested through the chat options so the library's own strict transform produces the schema; the response-format override is removed
- [x] `provider.require_parameters = true` is still sent
- [x] The existing request-shape unit test also asserts the schema contents: objects forbid additional properties and all properties are required
- [ ] Verify: re-run `just golden`; note per-video result in Comments, and whether the `error` failures disappear

## Comments
