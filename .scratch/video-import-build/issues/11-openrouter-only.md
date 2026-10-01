# 11: OpenRouter-only Video Import, adapter seam kept

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · supersedes the Ollama half of [08](08-ollama-adapter-local-dev.md) and decision [LLM providers](../../video-import/issues/02-llm-providers-ollama-openrouter.md)

**What to build:** Video Import runs on OpenRouter only. Local Ollama is dropped: it is unbearable because of the coil whine noise. `just dev` and compose use OpenRouter with the key from the git-ignored `.env`. The provider seam stays, so another provider is one enum value, one factory case and one validator rule.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] Ollama adapter, its wrappers (`think:false`, `keep_alive`, `truncate:false`) and the OllamaSharp package are deleted
- [ ] `ContextTokens` option and its validation are deleted (Ollama-only)
- [ ] Provider seam kept: `LlmProvider` enum (OpenRouter only), factory switch on it, per-provider validator rules, per-provider default model and endpoint
- [ ] Default provider is OpenRouter; startup fails clearly if `Llm:ApiKey` is missing or the provider is unknown
- [ ] Compose drops `host.docker.internal` `extra_hosts`; justfile drops the localhost Ollama rewrite
- [ ] `.env.example` documents OpenRouter only (key and optional model, base URL, timeout)
- [ ] Unit tests trimmed to OpenRouter: provider selection, validator, extractor; context-overflow matching in the extractor still covered
- [ ] Manually verified: one real Video Import on OpenRouter; noted in Comments
- [ ] Coverage gate passes
