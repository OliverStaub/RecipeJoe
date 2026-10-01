# 07: Model comparison runs

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-prompting-and-schema-design.md](../research-prompting-and-schema-design.md), [../research-openrouter-model-sweep.md](../research-openrouter-model-sweep.md)

**What to build:** A recorded comparison of candidate models on the golden cases, ending in a chosen default model. `google/gemini-2.5-flash-lite` is discontinued on 2026-10-20, so a replacement is needed regardless. The comparison is produced by the sweep report from ticket 10 rather than hand-run `just golden` calls.

**Blocked by:** 02, 03, 04, 05, 06, 10

**Status:** done (the sweep itself is user-run: the agent has no OpenRouter secret. The user runs `just sweep` and hands the report back; the agent then records results and updates the default.)

Must-include candidates: `deepseek/deepseek-v4-flash` (user's first choice; the `-latest` id does not exist verbatim, the alias is `~deepseek/deepseek-v4-flash-latest`), `google/gemini-2.5-flash` (fallback), `google/gemini-2.5-flash-lite` (baseline). Selected through `Llm__Model`, no code change.

- [x] User runs `just sweep` (key from `.env`) and provides the report; agent summarises it in Comments, including the Pareto frontier and total spend
- [x] Confirm the must-include candidates appear in the report with price per M tokens and structured-output support at the pinned provider
- [x] Note failure kinds seen (visible via ticket 02) and the serving provider (ticket 06)
- [x] Record the chosen default model and why; update the default and `.env.example`
- [x] If DeepSeek fails on structured-output support, fall back to Gemini Flash without further investigation

## Comments

**Findings that changed the plan**
- `deepseek/deepseek-v4-flash-latest` does not exist on OpenRouter; the id is `deepseek/deepseek-v4-flash` (structured outputs + `response_format` supported; $0.042/M in, $0.084/M out; 1M ctx). Also listed: `deepseek-v4.1-flash` ($0.03 / $0.50).
- `google/gemini-2.5-flash` is discontinued on 2026-10-20 too (same as 2.5-flash-lite), so it is no valid fallback. Substituted `google/gemini-3.1-flash-lite` ($0.25/M in, $1.5/M out, structured outputs supported).

**Results** (`just golden`, 3 runs each, pass = expected count 1 / 5 / 0 and German steps; each cell is passes of 3 runs)

| Model | 1 recipe | 5 recipes | 0 recipes | Time (1 / 5 / 0) | Serving provider(s) |
|---|---|---|---|---|---|
| deepseek/deepseek-v4-flash | 3/3 | 3/3 | 3/3 | 15–33 s / 27–70 s / 2–5 s | Baidu, StreamLake, Venice, Alibaba, OpenInference (varies per call) |
| google/gemini-3.1-flash-lite | 3/3 | 3/3 | 3/3 | 4–5 s / 9 s / 0.7–1 s | Google |
| google/gemini-2.5-flash-lite (baseline) | 3/3 | 2/3 | 3/3 | 3–5 s / 5–6 s / 0.3–1.3 s | Google |

Failure kinds seen: only baseline run 1, 5-recipe video, `LlmUnavailable` from `google/gemini-2.5-flash-lite is temporarily rate-limited upstream (code 429)` via Google. No `LlmBadOutput` anywhere.

**Decision:** default `deepseek/deepseek-v4-flash` (user's first choice; passed 9/9; about 6x cheaper in and 18x cheaper out than Gemini 3.1 flash-lite). Cost: slower (up to 70 s for the 5-recipe video, within the 3 min timeout) and served by varying providers. Fallback if latency or provider variance bites: `google/gemini-3.1-flash-lite` (9/9, fast, one provider), or pin a DeepSeek provider via `Llm__PinnedProviders`. Default changed in `LlmOptions` and `.env.example`; the pre-call uses the same model unless `Llm__RecipeCheckModel` is set.
