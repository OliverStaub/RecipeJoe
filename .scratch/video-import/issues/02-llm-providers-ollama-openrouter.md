# LLM providers: Ollama + OpenRouter

Type: research
Status: resolved
Blocked by: —

## Question

What do Ollama (local Gemma on macOS) and OpenRouter offer for turning a transcript into structured Recipe JSON? Cover: reaching host Ollama from the compose backend container on macOS (host.docker.internal, OLLAMA_HOST binding); which Gemma variants/sizes fit a typical Mac, their context windows and speed; structured output (JSON schema / `format`) in Ollama and OpenRouter; whether both speak an OpenAI-compatible chat API (one adapter + base URL, or two adapters?); .NET client options (Microsoft.Extensions.AI, OpenAI SDK, raw HttpClient); OpenRouter pricing for a suitable cheap model and API key handling; timeouts for long generations. Recommend the adapter shape.

Research: [`research/llm-providers-ollama-openrouter.md`](../../../research/llm-providers-ollama-openrouter.md) (no branch: repo guardrail blocks agent git writes)

## Answer

- Installed locally: `gemma4:26b` (MoE, 3.8B active, Q4_K_M, 256K native ctx) on Ollama 0.33.3; M3 Pro 36 GB; Docker via Colima. Gemma 4 (Apr 2026) is latest.
- Reach: `http://host.docker.internal:11434` works from compose container (add `extra_hosts: host.docker.internal:host-gateway`); keep Ollama bound to 127.0.0.1.
- Speed: ~40–47 tok/s gen, ~470 tok/s prompt, 12 s cold load → est. 60–80 s per 30-min video. `OLLAMA_NUM_PARALLEL=1` → concurrent Imports queue.
- Structured output: both Ollama native `format` and `/v1` json_schema produced valid German 2-recipe JSON. OpenRouter: depends on routed provider; use `provider.require_parameters: true`; `:free` variant lacks it.
- Recommended shape: domain seam `IRecipeExtractor` (owns prompt, schema, validation, timeout; faked in API/E2E) over provider seam `IChatClient` (Microsoft.Extensions.AI 10.10, `GetResponseAsync<T>`; faked in extractor unit tests). Two thin factories by `Llm:Provider`: OllamaSharp 5.5 (native API → per-request `num_ctx`, `think:false`, `keep_alive`) and OpenAI SDK 2.14 → OpenRouter base URL. Fallback: OpenAI SDK for both.
- OpenRouter: `google/gemma-4-26b-a4b-it`, $0.09/$0.30 per M tokens ≈ $0.0015/Import. Key in user-secrets / git-ignored `.env`, credit-limited; never in CI.
- Risks: silent context truncation (guard with `num_ctx` + token check); 100 s default timeouts too short (config per-call timeout, cap SDK retries); `require_parameters` passthrough via SDK untested; schema-valid ≠ usable Recipe; measurements from one toy transcript.
