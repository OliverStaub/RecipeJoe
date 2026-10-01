# 08: Ollama adapter + local dev

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [LLM providers](../../video-import/issues/02-llm-providers-ollama-openrouter.md) · research: [llm-providers-ollama-openrouter.md](../../../research/llm-providers-ollama-openrouter.md)

**What to build:** `just dev` runs Video Import against local Gemma on host Ollama at no cost. Switching to OpenRouter is a config change with the key in the git-ignored `.env`. Together with 07, a real YouTube link becomes German Recipes locally.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] OllamaSharp `IChatClient` factory for `Llm:Provider=Ollama` (native API, `think:false`, `keep_alive`)
- [ ] Defaults: Ollama, `gemma4:26b`, `http://host.docker.internal:11434`; OpenRouter model `google/gemma-4-26b-a4b-it`
- [ ] Compose backend gets `extra_hosts: host.docker.internal:host-gateway`; Ollama stays bound to 127.0.0.1 on the host
- [ ] `.env.example` documents the `Llm:*` overrides and the OpenRouter key; `.env` passed to the backend through compose; never used in CI
- [ ] Startup fails clearly on invalid LLM config (unknown provider, missing key for OpenRouter)
- [ ] Unit test: provider selection by config
- [ ] Manually verified: one real Video Import on Ollama and one on OpenRouter; noted in Comments
- [ ] Coverage gate passes
