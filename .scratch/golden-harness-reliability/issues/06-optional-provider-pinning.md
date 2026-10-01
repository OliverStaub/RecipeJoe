# 06: Optional provider pinning

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-openrouter-structured-outputs.md](../research-openrouter-structured-outputs.md)

**What to build:** A developer can pin which OpenRouter upstream provider serves requests through configuration only, so golden runs are reproducible and a failure can be attributed to a provider. Unset means today's behaviour.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] Optional setting (env var, documented in `.env.example`) for provider order/only, with fallbacks disallowed when pinned
- [x] Validated on start like the other LLM options
- [x] Request-shape unit test: pinned → provider preferences sent alongside `require_parameters`; unset → unchanged
- [x] Golden output logs which provider served each call when known

## Comments

- Done: `Llm__PinnedProviders` (comma separated, in order) → `provider: {require_parameters: true, order: [...], allow_fallbacks: false}`; unset → `{require_parameters: true}` as before. Validated on start (no blank/empty names). Documented in `.env.example`.
- Golden/extractor now logs `<Call> answered by model M via provider P` (read from the reply's top-level `provider` field via the SDK's JsonPatch).
- Seen in practice (DeepSeek, unpinned): the serving provider changes between calls (Baidu, StreamLake, Venice, Alibaba, OpenInference). Not pinned in the default; set `Llm__PinnedProviders` if a provider turns out flaky.
