# 09: Single-model golden run, scored and costed

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-openrouter-model-sweep.md](../research-openrouter-model-sweep.md)

**What to build:** Run the golden cases against one given model id and get a machine-readable result: pass/fail per case, failure kind, serving provider, token counts and the real cost. The request is pinned to one provider with required-parameter routing so results are not blended across providers.

**Constraint:** The agent has no access to the OpenRouter secret and cannot run live calls. Everything is built and verified offline (fake client, recorded fixtures). The user runs the real thing; the tool reads `Llm__ApiKey` from the environment, which `just` already loads from `.env`. Never read, print or log the key.

**Blocked by:** None for the code. Results are only meaningful once 03, 04 and 06 are done.

**Status:** code done, waiting for the user-run smoke check

- [x] Runnable as `just sweep-model <model-id>`; fails fast with a clear message if `Llm__ApiKey` is missing, without echoing it
- [x] Verify from the client code and a recorded response fixture whether `usage.cost` and the serving provider are surfaced; if not, capture them (fall back to the generation stats endpoint)
- [x] Input: model id, optional provider, repeat count; output: one JSON result per model
- [x] Per case: pass/fail against expected recipe count, failure kind (using ticket 02 errors), tokens in/out, cost in USD
- [x] Requests use `require_parameters` and pin the provider (ticket 06)
- [x] A 402 (in-flight budget) is retried with backoff; other provider errors are recorded as failures, not crashes
- [x] Reads the API key from the environment; documented that a dedicated key with a spend limit should be used
- [x] Unit tests with a fake client for scoring, result shape, 402 retry and error handling (agent-verifiable, no secret)
- [ ] User-run smoke check (not agent-verifiable): one live run against a single cheap model produces a result file with a non-zero cost; user records the outcome in Comments

## Comments

- `just sweep-model <id> [--provider NAME] [--repeats N]` writes `backend/sweep-output/results/<id>.json`. Missing `Llm__ApiKey` exits 2 with a message that doesn't echo it.
- `usage.cost` and the serving provider are surfaced: `LlmClientFactory.Cost` reads `usage.cost` from the reply's raw `ChatCompletion` (same way as `ServingProvider`), tokens come from the library's `Usage`. Verified against a local stub reply, not a recorded live one: the stub's `usage` block follows the documented shape, so the user's smoke run is the real confirmation. If `cost` comes back 0, use the generation stats endpoint as the fallback.
- Failure kinds are the extractor's `ImportFailure` names plus `WrongCount`. 402 retried up to 5 times with 2/4/8/16/32 s backoff, other errors recorded.
- A dedicated key with a spend limit is documented in `tests/RecipeJoe.Sweep/README.md`.
- **Still open (user-run):** one live `just sweep-model <cheap model>`; record outcome and non-zero cost here.
