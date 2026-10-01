# OpenRouter price list and a ~100-model golden sweep

Doc pages were fetched with WebFetch, which returns summaries made by a small model. Quotes marked "per fetch" are paraphrased. Figures marked **[live]** were computed from real responses of `GET https://openrouter.ai/api/v1/models` and `/models/{author}/{slug}/endpoints` fetched with curl on 2026-10-01 (unauthenticated). The catalog changes daily, so numbers are a snapshot.

## Bottom line

**Yes, feasible, cheap, and worth doing in two stages.** Roughly 250 models in the catalog are plausible candidates for this app's task (text in, strict JSON out). A first pass of all of them costs on the order of **$10-$75** at a single run each. Assumptions: about 15.5k input and 6-18k output tokens per `just golden` pass, which is the estimate method below. Pre-filtering to the ~100 cheapest-to-mid models with 3 repeats is likely **$5-$20**. The 25 most expensive models (the `-pro` and `o1`/`o3` families) account for most of the spend and can be excluded by price cap.

Caveats:
- **The task is text-only.** The golden harness replays recorded transcripts, not video. The "video modality" filter is not needed; filter on text input plus structured output. See "What the harness actually sends".
- **`supported_parameters` on a model is the union over its providers.** Without `provider.require_parameters: true` (and ideally pinning) a request can land on an endpoint without structured outputs. [live]: `deepseek/deepseek-v4-flash` lists `structured_outputs`, yet 5 of its 15 endpoints do not.
- **Three runs per video is noise-prone, not statistics.** There are only 3 videos and 3 pass/fail checks. A 100-model leaderboard from this set is coarse. Add golden cases before trusting fine ranking.
- **Free (`:free`) models are not usable for a sweep.** Cap is 50 requests/day under $10 credits purchased, 1000/day above, and failed requests count (https://openrouter.ai/docs/api-reference/limits).

## 1. Price list

### Endpoints
- `GET https://openrouter.ai/api/v1/models` is public, needs no key, and returned **463 models** [live] in one response (~760 KB, no pagination needed). Documented as supporting `offset`/`limit`, plus filters `output_modalities` and `supported_parameters` (per fetch: https://openrouter.ai/docs/guides/overview/models). [live]: `?supported_parameters=structured_outputs` returns 200 and 358 models; client-side filtering of the full list gave 380 (the server filter's semantics differ slightly, so filter client-side and re-check).
- `GET /api/v1/models/{author}/{slug}/endpoints` lists per-provider endpoints with their own pricing and `supported_parameters` [live].
- `GET /api/v1/endpoints/zdr` lists zero-data-retention endpoints [live, public, 200].
- Each model carries `expiration_date` (non-null means the endpoint is being deprecated; 25 models have it [live]). Relevant: ticket 07 says `google/gemini-2.5-flash-lite` is discontinued 2026-10-20.

### Pricing fields
Per https://openrouter.ai/docs/guides/overview/models (per fetch) and [live] responses:
- Values are **strings, USD per single unit**, not per million. `"0.0000001"` for `prompt` is $0.10 per 1M tokens. Multiply by 1e6 for display.
- Keys seen [live]: `prompt`, `completion` (all 463), `input_cache_read` (298), `web_search` (177, per search), `input_cache_write` (94), `overrides` (80), `input_cache_write_1h` (33), `audio` (33), `internal_reasoning` (31), `image` (30), plus `request`, audio-cache, image/audio output.
- `"-1"` means variable price: the routers (`openrouter/auto`, `openrouter/pareto-code`, `openrouter/fusion`, `~`-style routers, `typesafe/jev-router`) [live]. Exclude them from a sweep; the model actually used varies.
- `pricing.overrides` encodes conditional rates, for example `min_prompt_tokens: 272000` raising prompt and completion price; also time-based windows (per fetch of the models guide). Irrelevant for ~15k-token prompts unless a long video transcript is used.
- Reasoning tokens are billed as completion tokens at `completion` price unless `internal_reasoning` is set (31 models). Verify via `usage.cost`, not by formula.
- Top-level model `pricing` is one headline number; actual price depends on the serving endpoint (below).

### Variants in the catalog
https://openrouter.ai/docs/guides/routing/model-variants (per fetch):
- `:free` and `:batch` appear as separate catalog entries (16 `:free` [live]). `:batch` is half price but for the Batch API only. Exclude both.
- `:nitro` (throughput sort, priority tier, possibly higher price) and `:floor` (price sort, flex tier, possibly lower price) are routing suffixes and do **not** appear in the catalog.
- `:exacto` routes on tool-calling quality signals. Not about structured output.
- `:thinking`, `:extended`, `:online` are deprecated.

### Price varies a lot per provider
[live] from `/endpoints` (prices in $/M input / output):
- `deepseek/deepseek-v4-flash`: 15 endpoints, from 0.004/1.28 (Relace) and 0.042/0.084 (Baidu) up to 0.21/0.56 (Azure).
- `deepseek/deepseek-v3.2`: 13 endpoints, 0.209/0.31 up to 3.0/4.5.
- `openai/gpt-oss-120b`: 23 endpoints, 0.03/0.17 to 0.35/0.75.
- `google/gemini-2.5-flash`: 0.15/1.25 (AI Studio flex) up to 0.54/4.5 (priority). Endpoint `tag` values like `google-ai-studio/flex` and `/priority` expose the service tier.
- Consequence: the default routing is "price-based load balancing" weighted inversely by squared price (https://openrouter.ai/docs/guides/routing/provider-selection, per fetch). So repeated runs of one model may hit different providers at different quantizations (fp4/fp8/bf16 vary per endpoint [live]). For a sweep, pin the provider or record `provider_name` per request.

### Filtering for this app's task
Fields: `architecture.input_modalities`, `architecture.output_modalities`, `supported_parameters` (`structured_outputs`, `response_format`), `context_length`.
[live] counts across all 463:
- `structured_outputs`: 380; `response_format`: 397 (35 have `response_format` without `structured_outputs`, i.e. JSON mode only; probably not strict schema).
- Input includes `video`: 85 models; with `structured_outputs`: 76. Image input plus structured outputs: 262.
- Text in/out + `structured_outputs`, excluding `:free`, `:batch`, routers, `~` aliases, expiring models, context < 32k: **252 candidates**. 180 of them advertise `reasoning`; 212 advertise `seed`.
- With completion price <= $5/M: 177. With <= $1/M: 95.
- 255 models carry a `benchmarks` object (Artificial Analysis `intelligence_index` etc.) [live]. See section 5.

## 2. Measuring actual cost

- Every response includes a `usage` object automatically. Fields: `prompt_tokens`, `completion_tokens` (native tokenizer), `cost` (credits charged), `cost_details.upstream_inference_cost` (BYOK only; otherwise 0/null), `reasoning_tokens`, `cached_tokens`, `cache_write_tokens`. The old `usage: {include: true}` flag has no effect (https://openrouter.ai/docs/guides/guides/usage-accounting, per fetch). When streaming, usage is in the last SSE chunk.
- `GET /api/v1/generation?id=gen-...` returns `total_cost`, `tokens_prompt/completion`, `native_tokens_prompt/completion/reasoning/cached`, `latency`, `generation_time`, `provider_name`, `model`, `native_finish_reason`, `finish_reason`, `provider_responses` (fallback attempts), `service_tier` (https://openrouter.ai/docs/api-reference/get-a-generation, per fetch). The docs state nothing about availability delay, so retry on 404.
- Recommendation: read `usage.cost` from the chat response per extraction call, sum per model per run, and log `provider`/`model` from the response. Use `/generation` only to fill in `provider_name`/`native_finish_reason` after the fact. Whether the existing `LlmClientFactory`/Microsoft.Extensions.AI path exposes `usage.cost` is UNVERIFIED; the OpenAI-compatible client may drop unknown fields, so a raw `HttpClient` call or `AdditionalProperties` inspection may be needed.
- Key-level tracking: `GET /api/v1/key` returns `limit`, `limit_remaining`, `limit_reset` (per fetch, https://openrouter.ai/docs/api-reference/limits).
- Billing has no inference markup but credit purchase costs 5.5% ($0.80 minimum) by card, 5% crypto (https://openrouter.ai/docs/faq, per fetch). Minimum purchase $5 (https://openrouter.ai/terms, per fetch). Unused credits expire after 365 days.

## 3. Limits, credits, policy

- Balance must stay positive for any model; negative gives `402` (https://openrouter.ai/docs/api-reference/limits, per fetch).
- Three credit gates: account balance, optional per-key limit (`limit_source: openrouter_key_limit`), and an **in-flight spending budget** that reserves estimated cost of running requests. Too many concurrent expensive requests get `402` with `reason: in_flight_budget_exhausted` and `Retry-After`; this is transient. Expensive models with a large `max_tokens` reserve more, so set a modest `max_tokens` and handle this 402 as retry.
- Paid models: no published per-minute/day cap; Cloudflare DDoS protection applies. Capacity is governed globally; "additional accounts or API keys will not affect your rate limits". 429s carry `X-RateLimit-*` and `Retry-After`.
- Free models: 20 req/min; 50/day (<$10 purchased) or 1000/day (>= $10). Failed requests count.
- **Recommended safety:** create a dedicated API key with a `limit` of e.g. $10-$20 before the sweep.
- ToS (https://openrouter.ai/terms, per fetch): no benchmarking clause or rate-abuse clause found. Prohibited are scraping by automated means, reselling API access, and building a competing service. Output ownership is governed by each Model's own terms. A private evaluation of your own prompts is not addressed by any of these. Per-model/provider terms could still restrict benchmark publication; if results are only internal, this is low risk. UNVERIFIED for individual model licences.
- Data policy: `provider.data_collection: "deny"` and `provider.zdr: true` restrict routing to compliant endpoints (provider-selection page). Recordings contain only public YouTube transcripts, so this is low priority, but zdr shrinks the endpoint pool and can change price.

## 4. Practicalities of ~100 models

### What the harness actually sends
`backend/tests/RecipeJoe.GoldenTests/GoldenCheckTests.cs` runs 3 recorded transcripts (`Recordings/*.json`: 9.3 KB, 33 KB, 1.3 KB; expected 1, 5, 0 recipes) through `RecipeExtractor` with `LlmClientFactory`. Config: `Llm__Model`, `Llm__ApiKey`, `Llm__BaseUrl`, `Llm__Timeout`. `just golden` runs it. So a sweep is a loop over `Llm__Model` values with no code change to the extractor (matches ticket 07: "Selected through `Llm__Model`, no code change"). Input is text only. Ticket 07 limits itself to 3 candidates and "a handful of runs"; this note is about scaling that.

### Cost estimate method
Per model: `cost = prompt_price * T_in + completion_price * T_out`, summed over the 3 videos.
- `T_in`: file bytes / ~4 chars per token for the transcripts (~2.3k + ~8.3k + ~0.3k = ~11k) plus prompt and schema per call (~1.5k x 3). Total about **15.5k input tokens** per golden pass. UNVERIFIED approximation; replace by `usage.prompt_tokens` from one real run.
- `T_out`: 6 recipes of JSON, roughly **6k** output tokens non-reasoning; I used up to **18k** for reasoning-heavy models.
- Result [live prices, 252 candidates]: sum of one pass over all candidates = **$25.0** (median model $0.018, cheapest $0.0005, most expensive `openai/o1-pro` $5.92, next ones `gpt-5.5-pro` $1.55). With 18k output tokens and 3 repeats: **~$172** for all 252; excluding models above $5/M output (177 models remain): **~$16**. The 95 models at <= $1/M output cost a few dollars at 3 repeats.
- Better: after a 1-run cheap pass, replace estimates with measured `usage.cost`.

### Pre-filtering to shrink to ~100
1. Text input, text output, `structured_outputs` in `supported_parameters` (380 -> ~250 after dropping `:free`, `:batch`, routers, aliases, expiring, <32k context).
2. Context length >= what the long transcript needs (the 33 KB transcript is ~10k tokens; modest).
3. Price cap (e.g. completion <= $5/M or total estimated cost per pass <= $0.10) to drop pro-tier models.
4. Provider has a structured-output endpoint: check `/endpoints` for at least one endpoint with `structured_outputs`, then pin it (`only`) rather than trust the model-level union.
5. Keep one model per family/size (e.g. drop dated duplicates, `-preview`) and use `benchmarks.artificial_analysis.intelligence_index` where present (255 models) to drop clearly weak ones.
6. Drop models without `seed`/`temperature` support if reproducibility matters (212 of 252 have `seed`).

### Concurrency
No published paid rate limit, so concurrency is bounded by the in-flight budget (402 transient) and per-provider 429s. Run models sequentially or with small parallelism (e.g. 4-8 models at once, 3 videos serial per model) and retry 402/429 with `Retry-After`. The harness is an MSTest project, so a parallel sweep is better driven by a script looping over model ids, each invoking `just golden` (or a tiny console runner) with a different `Llm__Model`, writing JSON per model: pass/fail per video, `usage.cost`, tokens, `provider_name`, latency.

### Nondeterminism
- Repeat at least 3 times per model and report pass rate (k of 9), not one run. Set `temperature` explicitly (0 or the app's production value). `seed` is advertised for most models but is best-effort across providers; I found no OpenRouter statement guaranteeing determinism (UNVERIFIED).
- Count `finish_reason: "error"` and invalid JSON separately from semantic failures (see research-openrouter-structured-outputs.md, items 1-2), otherwise provider flakiness gets scored as model weakness.

### Provider routing and pinning
- Pin per model: `provider: {only: ["<slug>"], allow_fallbacks: false, require_parameters: true}` (provider-selection page, per fetch). For the production default you probably want the config you benchmarked, so benchmark the pinned config. Ticket 06 (optional provider pinning) is a prerequisite for a clean sweep.
- `:floor` gives the cheapest provider (flex tier) and `:nitro` the fastest (priority tier); both change price and possibly quantization. Use `:floor` as a separate sweep arm only if production will use it. A price comparison across `:floor` and default routing is a good second step because per-provider price spread is 10x or more [live].
- Record `provider_name` from `/generation` or the response for every call.

## 5. Finding the sweet spot

- Method: for each model compute **score** = pass rate over (3 videos x N repeats) plus quality checks, and **cost** = mean measured `usage.cost` per golden pass (and per extraction). Plot score vs cost, keep the **Pareto frontier** (no other model is both cheaper and at least as good). Choose the cheapest frontier point above a minimum bar (e.g. all 3 videos pass in >= 90% of runs). Consider latency/p95 as a third axis because Video Import is interactive; `/generation` returns `latency` and `generation_time`.
- Two-stage design: (1) one cheap pass of all ~250 filtered candidates to eliminate those failing the structured-output contract or the 0/1/5 counts; (2) 3-5 repeats with pinned providers on the ~10-20 survivors, and ideally more golden videos.
- Score sensitivity: the 0-recipe case and the exact-count check are pass/fail on 3 samples, so many models will tie at 3/3. Add more golden cases (more videos, varied languages) before trying to separate close models, otherwise cost alone picks the winner.
- First-party OpenRouter helpers (all optional):
  - `benchmarks.artificial_analysis.intelligence_index` and `design_arena` fields in `/models` [live]. They are third-party scores relayed by OpenRouter (https://openrouter.ai/docs/guides/overview/models, per fetch). They measure general capability, not this extraction task; use only for pre-filtering.
  - **Auto Router** picks a model per prompt from aggregate community spend over a trailing 7 days, with `low` to `max` cost tiers and allow/exclude lists (https://openrouter.ai/docs/guides/routing/routers/auto-router, per fetch). It is unsuitable for a benchmark: the model varies per request, and its price in `/models` is `-1`. It could be a candidate for the production default only if you accept a changing model.
  - **Presets** (`@preset/name`, or `model@preset/name`) bundle model, provider routing, system prompt and parameters on the OpenRouter side (https://openrouter.ai/docs/guides/features/presets, per fetch). They would let you switch the production model without a deploy, but a sweep via `Llm__Model` env is simpler and keeps config in the repo.
  - Model rankings pages show popularity; I did not find a primary-source API for them beyond the Auto Router description, so do not rely on them for selection.

## Unresolved / unverified
- Does the app's OpenAI-compatible client surface `usage.cost`? (UNVERIFIED; check `LlmClientFactory`.)
- Exact input/output token counts per golden pass (estimated; measure with one real run).
- Whether strict schema is actually enforced per provider for each candidate model (OpenRouter says only "for providers with a native strict mode"; https://openrouter.ai/docs/features/structured-outputs). The sweep itself measures this empirically.
- Individual model licence terms regarding publishing benchmark results (internal use only is low risk).
- User's first-choice id `deepseek/deepseek-v4-flash-latest` from ticket 07 does not exist verbatim [live]; the catalog has `deepseek/deepseek-v4-flash`, `deepseek/deepseek-v4-flash-0731`, `deepseek/deepseek-v4.1-flash`, and the alias `~deepseek/deepseek-v4-flash-latest`. `deepseek/deepseek-v4-flash` lists `structured_outputs` and `require_parameters`-relevant endpoint support is mixed (10 of 15 endpoints).
