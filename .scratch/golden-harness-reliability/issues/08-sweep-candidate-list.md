# 08: Sweep candidate list from the OpenRouter price list

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-openrouter-model-sweep.md](../research-openrouter-model-sweep.md)

**What to build:** A script you run yourself (`just sweep-candidates`) that pulls the public OpenRouter model list and writes a filtered candidate report with prices. No LLM calls, no spend. Implemented as a .NET console project beside the golden tests so later tickets can share its code.

**Note:** The models endpoint is public, so no secret is needed and the agent can run this one end to end.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Fetches the public models endpoint; no API key needed
- [x] Keeps only models with text input and output and `structured_outputs` in supported parameters; drops `:free`, `:batch`, routers (negative prices) and models with an expiry date
- [x] Prices parsed as per-token strings and shown as USD per M tokens
- [x] Estimates cost per golden pass from assumed tokens per case (assumptions configurable and printed in the report)
- [x] Optional filters: max output price per M tokens, name pattern
- [x] Writes a markdown (and CSV) report sorted by estimated cost, with candidate count and the filters applied
- [x] Unit tests for filtering and price parsing against a recorded sample of the API response

## Comments

- Built as `backend/tests/RecipeJoe.Sweep` (console project; also holds the sweep code of 09/10 and the golden case list shared with `just golden`). `just sweep-candidates` ran against the live list: 106 candidates of 463 with `--max-output-price 1`.
- Also drops `~` aliases and `openrouter/*` routers (`openrouter/free` has price 0, so the negative-price rule missed it). `--include id,id` keeps named models regardless of filters.
- **Finding:** `google/gemini-2.5-flash` and `google/gemini-2.5-flash-lite` both carry `expiration_date` 2026-10-20 in the live list, so the expiry filter drops them. Use `--include` to keep them as reference in a sweep.
- Test fixture is a trimmed copy of the real list (2026-10-01) in `UnitTests/Sweep/models-sample.json`.
