# Long videos vs context window

Type: research
Status: resolved
Blocked by: —

## Question

How long do real recipe-video transcripts get (tokens), and what happens when the prompt exceeds the model's context? Facts needed: Ollama's default `num_ctx` and whether it truncates silently; how to set `num_ctx` per request via OllamaSharp / `IChatClient` options; `gemma4:26b` max context and memory/latency cost of larger contexts on the dev machine; OpenRouter model context limits and its behaviour on overflow (error vs middle-out transform); cheap token counting in .NET before sending. Measure the candidate videos (i84Sc5uvQa8, 6tMZNYQkycI with 10 recipes, 6wR2T-PexT4 long DE). Recommend: raise `num_ctx` + token-count guard (fail with a typed kind) vs chunking.

Research: [`research/long-videos-context-window.md`](../../../research/long-videos-context-window.md)

## Answer

- **Recommendation**: fixed `num_ctx` from config (`Llm:ContextTokens`, default 32768) + `truncate:false` so overflow is an HTTP 400, mapped to new non-retryable kind `VideoTooLong`. No chunking in V1.
- **Sizes (Gemma tokens, title+desc+transcript)**: `i84Sc5uvQa8` 5 min EN 2.3k; `6tMZNYQkycI` 13 min 0.4k (music-only, transcript is `[Music]` — not a length issue, a content one); `6wR2T-PexT4` 36 min DE 9.2k. ≈ 250–430 tok/min → 32K fits ~1 h + output.
- **Ollama default**: VRAM-based (<24 GiB 4k, 24–48 GiB 32k; this Mac 32k). Overflow is **silent**: HTTP 200, prompt cut to ~num_ctx/2 by dropping the *front* (system prompt/title lost); only a server-log WARN. `truncate:false` → 400 `exceed_context_size_error` with `n_prompt_tokens`.
- **.NET**: `num_ctx` via `ChatOptions.AdditionalProperties["num_ctx"]` (OllamaSharp 5.5.0). OllamaSharp has no `truncate` field → `DelegatingHandler` injects `"truncate":false` into `/api/chat`; verified → `OllamaException`. Keep `num_ctx` constant (each change reloads model, 3–10 s).
- **Cost of ctx**: KV ≈ 20 MiB/1k tokens + 300 MiB fixed (32K ≈ 0.95 GB, 256K ≈ 5.4 GB); prompt latency depends on prompt length only (9.2k → ~20 s), not on `num_ctx`.
- **OpenRouter** gemma-4-26b: 262K ctx; overflow → request error unless `context-compression` plugin (middle-out; default only on ≤8K endpoints) — keep it off.
- **Token counting**: Ollama has no tokenize endpoint; exact count comes back in 400/usage. Optional pre-estimate `chars/3` (measured 3.2–3.9 chars/token).
