# OpenRouter structured outputs and reliability (google/gemini-2.5-flash-lite)

Sources were fetched with WebFetch, which returns summaries made by a small model. Quotes marked "per fetch" are therefore paraphrased, not verbatim. Re-check the key ones in a browser before relying on them.

## Summary (ranked recommendations)

1. **Treat `finish_reason: "error"` as a retryable transient failure.**
   - Retry once or twice with backoff.
   - Check the body for a top-level `error` even on HTTP 200.
   - Make the SDK/parser tolerate the `error` enum value (a null/unknown finish_reason must not throw), or parse the raw JSON yourself.
   - Source: https://openrouter.ai/docs/api-reference/errors
2. **Validate the reply client-side against the schema and retry on violation.**
   - OpenRouter does not validate output.
   - Strict mode is only enforced "for providers with a native strict mode" (https://openrouter.ai/docs/features/structured-outputs).
   - Null `ingredientLines` and `steps` are therefore not a contradiction of the docs. The model can violate the schema even when the request says strict.
   - Count these retries as harness noise, not as model failures.
3. **Add `plugins:[{id:"response-healing"}]` (non-streaming only).**
   - It fixes syntax-level damage: markdown fences, trailing commas, missing brackets.
   - It does not fix semantic violations such as nulls, wrong shape, or `[]` instead of `{"recipes":[]}`. UNVERIFIED for those cases, because the docs only list syntax fixes.
   - It cannot repair output truncated by `max_tokens`.
   - Source: https://openrouter.ai/docs/guides/features/plugins/response-healing
4. **Pin the provider, to make runs reproducible and attributable.**
   - Both `google-ai-studio` and `google-vertex` serve this model with structured outputs.
   - Set `provider.order:["google-ai-studio"]` with `allow_fallbacks:true`, or `only:[...]` for strict pinning. Compare Vertex vs AI Studio by rerunning with `only` on each.
   - Source: https://openrouter.ai/docs/features/provider-routing
5. **Log `provider`, `id`, `model`, `native_finish_reason`, and the `error` object on every failure.**
   - Query `GET /api/v1/generation?id=gen-...` for `provider_name` and `native_finish_reason`.
6. **Set an explicit `max_tokens`.**
   - The docs state no default; the limit is context length minus prompt length.
   - Long transcript plus a big JSON reply risks `finish_reason: "length"`, which produces truncated, invalid JSON.
7. **Optionally add a `models` fallback list.**
   - It is only useful if a second model is acceptable for the golden test.
   - It would blur attribution, because the response `model` field changes.
8. **Simplify the schema for Gemini.**
   - Avoid `anyOf` and nullable unions where possible.
   - Prefer explicit `required` arrays and `additionalProperties:false`.
   - Gemini docs: not all JSON Schema features are supported, and large or deeply nested schemas may be rejected.
   - Keep the shape described in the prompt (already done in commit 73c4728).

## Q1. finish_reason "error"

Source: https://openrouter.ai/docs/api-reference/errors

- **Meaning (per fetch):** "When generation fails mid-stream, OpenRouter terminates the response with `"finish_reason": "error"`".
  - Errors after streaming has begun arrive as SSE. The HTTP status stays 200 because headers were already committed.
- **Body (per fetch):** "Check the body for an `error` field even on a `200`, rather than relying on the status alone."
  - The error metadata sits in the chunk or body alongside the `error` finish_reason.
  - UNVERIFIED: the exact JSON layout for non-streaming responses. I did not retrieve the page's example payload.
- **Normalised values:** `tool_calls`, `stop`, `length`, `content_filter`, `error`. The raw provider value is in `native_finish_reason` (https://openrouter.ai/docs/api-reference/overview).
  - So `error` is a documented value. An SDK enum that lacks it is the SDK's bug.
- **Empty content:** models can return empty responses "warming up from a cold start" or while scaling up. Reasoning models that exhaust their token budget return 200 with empty content and `finish_reason: "length"`.
- **Retry:** "Respect the `Retry-After` header before retrying." This applies to rate-limit and availability errors.
  - No specific recommendation was found for retrying `finish_reason: error` specifically. Retrying is my inference.
- **Fallbacks:** https://openrouter.ai/docs/guides/routing/model-fallbacks (per fetch)
  - "If the first model returns an error, OpenRouter will automatically try the next model in the list."
  - Any error can trigger fallback, including context length errors, moderation flags, rate limits and downtime.
  - Billing uses the model ultimately used, returned in the response `model` attribute.
  - UNVERIFIED: whether a mid-stream or finish_reason error (after a 200) triggers provider or model fallback. Mid-stream, headers are already committed, so likely not.

## Q2. Structured outputs

Sources: https://openrouter.ai/docs/features/structured-outputs and https://openrouter.ai/docs/api-reference/parameters

- **Request shape:** `response_format: {type:"json_schema", json_schema:{name, strict, schema}}`.
- **Strict semantics (per fetch):** "Set `strict: true` so that providers with a native strict mode enforce your schema exactly."
  - Enforcement varies: some providers guarantee compliance, others treat the schema as a strong hint.
  - Strict modes may limit which JSON Schema features are usable.
- **Support:** "Structured outputs are supported by select models", and support is per endpoint. The same model can differ across providers.
- **Guaranteeing endpoint support:** check supported parameters on the models page. Set `require_parameters: true` and include `response_format` type `json_schema`.
  - `require_parameters` only filters providers by declared parameter support. It does not add validation.
- **Does OpenRouter validate?** The docs describe pass-through to provider enforcement. They do not describe server-side validation. I found no statement that OpenRouter checks output against the schema.
- **Errors:** requests fail if the model lacks support or the schema is invalid.
- **Streaming:** supported. Response Healing applies to non-streaming only.
- **gemini-2.5-flash-lite endpoints** (https://openrouter.ai/api/v1/models/google/gemini-2.5-flash-lite/endpoints, per fetch):
  - Endpoints: google-vertex, google-vertex/eu, google-ai-studio, google-ai-studio/flex, google-ai-studio/priority.
  - Each lists structured outputs, response format and reasoning as supported. Max completion is 65,535 tokens and context is 1,048,576.
  - Uptime figures were snapshots: vertex 98.24%, vertex/eu 99.91%, AI Studio 99.56%, flex 100%.
  - Quantization is unknown.
  - The model page says it is being discontinued on October 20, 2026 (https://openrouter.ai/google/gemini-2.5-flash-lite). Plan a migration.
- **Gemini schema limits** (https://ai.google.dev/gemini-api/docs/structured-output, per fetch):
  - A subset of JSON Schema is supported. Types: string, number, integer, boolean, object, array, null.
  - `enum`, `anyOf`, `additionalProperties`, `required`, `$ref` recursion, and type arrays such as `["string","null"]` are all shown as supported.
  - "Very large or deeply nested schemas may be rejected."
  - UNVERIFIED: whether the fetched page covers `propertyOrdering` (the Gemini docs historically mention it) and whether it applies to 2.5 flash-lite specifically.
  - The page's examples used newer Gemini models. I did not confirm that the OpenRouter-to-Google translation passes all of these keywords through.

## Q3. Routing knobs

Source: https://openrouter.ai/docs/features/provider-routing (field table, per fetch)

| Field | Default | Notes |
|---|---|---|
| `order` | none | List of provider slugs to try in order |
| `only` | none | Allowlist |
| `ignore` | none | Blocklist |
| `allow_fallbacks` | `true` | Backup providers when the primary is unavailable |
| `sort` | none | `price`, `throughput` or `latency` |
| `require_parameters` | `false` | Only use providers that support all request parameters |
| `data_collection` | `allow` | `allow` or `deny` |
| `quantizations` | none | e.g. int4, fp8 |
| `zdr` | none | Zero-data-retention endpoints only |
| `max_price`, `preferred_min_throughput`, `preferred_max_latency` | none | Cost and performance filters |

- **Default load balancing:** prioritises providers without recent outages, then picks the lowest-cost candidates weighted by inverse square of price. The rest are kept as fallbacks. Without pinning, different runs can hit different providers, which explains flaky goldens.
- **`models` array:** priority-ordered fallback list (see Q1).
- **Response Healing:** see Summary item 3.
- **Reasoning** (https://openrouter.ai/docs/guides/best-practices/reasoning-tokens, per fetch):
  - The `reasoning` object supports `effort` (`none` to `max`), `max_tokens`, `exclude`, and `enabled`.
  - For Gemini 2.5, effort is mapped to Google's `thinkingBudget`.
  - Without parameters, the model's native default applies. UNVERIFIED: flash-lite's native default. Google historically makes thinking off by default on flash-lite, but I did not confirm this from a primary source.
  - To make behaviour deterministic, set `reasoning:{effort:"none"}` or `{max_tokens:0}`. UNVERIFIED that flash-lite accepts these.
  - Reasoning tokens count against the output budget, so heavy thinking can cause `length` with empty content.
- **max_tokens:** no default is stated. The limit is context length minus prompt length.
  - OpenRouter's normalised finish value is `length` (see Q1). Truncated JSON is not repairable by healing.
  - UNVERIFIED: the actual default applied upstream when `max_tokens` is omitted.

## Q4. Attributing a request to a provider

- `GET /api/v1/generation?id=<gen-id>` returns `provider_name`, `finish_reason`, `native_finish_reason`, `latency`, token counts, `model`, `request_id`, `streamed` and `cancelled` (https://openrouter.ai/docs/api-reference/get-a-generation, per fetch).
  - The `id` must match `^gen-[0-9A-Za-z-]+$`.
  - Metadata may lag the response slightly. UNVERIFIED, from experience rather than docs.
- Response body: `id` (the `gen-...` value), `model`, and `choices[].native_finish_reason` (https://openrouter.ai/docs/api-reference/overview).
  - A top-level `provider` field on the completion response: UNVERIFIED. The fetched docs did not mention it. Check a real response. I recall it being present, but have no primary-source confirmation.
- Recommended harness logging per call: `id`, `model`, `provider` (if present), `finish_reason`, `native_finish_reason`, the `error` object, and the raw body on failure.
