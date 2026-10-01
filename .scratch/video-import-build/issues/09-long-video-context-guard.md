# 09: Long-video context guard

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [Long videos vs context window](../../video-import/issues/09-long-videos-context-window.md) · research: [long-videos-context-window.md](../../../research/long-videos-context-window.md)

**What to build:** A video too long for the LLM's context fails cleanly with its own reason ("too long"), and can't be retried. Without this, Ollama silently cuts the front of the prompt (system prompt, title) and writes a wrong Recipe. Videos up to about an hour of speech fit.

**Blocked by:** 05, 08

**Status:** ready-for-agent

- [ ] `Llm:ContextTokens` (default 32768) sent as a constant `num_ctx` on every Ollama request (a change would reload the model)
- [ ] `"truncate": false` injected into Ollama chat requests (OllamaSharp lacks the field; e.g. a delegating handler), so overflow is a 400 `exceed_context_size_error` instead of silent truncation
- [ ] New failure kind `VideoTooLong`: Ollama context overflow and the OpenRouter context-length error map to it (OpenRouter `context-compression` stays off); not retryable (dismiss only)
- [ ] Video message: "Dieses Video ist zu lang, daraus kann kein Rezept gelesen werden."
- [ ] Extractor unit test: overflow reply → `VideoTooLong`; API test via the stub; route test: message + no retry button
- [ ] Manually verified with a too-small `Llm:ContextTokens` against real Ollama → `VideoTooLong`; noted in Comments
- [ ] Coverage gate passes
