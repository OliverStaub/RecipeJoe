# Research: Long videos vs the LLM context window

Date: 2026-10-01. Ticket: `.scratch/video-import/issues/09-long-videos-context-window.md`. Builds on [`llm-providers-ollama-openrouter.md`](llm-providers-ollama-openrouter.md) (§3 "Context window: a trap") and [`youtube-transcript-access.md`](youtube-transcript-access.md).

Legend: **[doc]** primary docs linked. **[src]** upstream source read (tag/branch named). **[api]** live first-party API response, 2026-10-01. **[measured]** run locally (Apple M3 Pro 36 GB, Ollama 0.33.3, `gemma4:26b` Q4_K_M, YoutubeExplode 6.6.2 / OllamaSharp 5.5.0 on .NET 10, home IP). **[inference]** my conclusion.

---

## 1. Summary

- **Recommendation: fixed, larger `num_ctx` + hard overflow error mapped to a typed failure kind. No chunking.** Real recipe videos are small next to Gemma 4's 256K window. Set `num_ctx` = 32768 by config, send `truncate:false` so Ollama answers **HTTP 400** instead of silently cutting the prompt, and map that (and OpenRouter's 400) to a new non-retryable kind, e.g. `VideoTooLong`.
- **Measured sizes** (title + description + transcript, Gemma tokenizer): 2.3k tokens (5 min EN), 0.4k (13 min, music-only), **9.2k (36 min DE)**. ≈ 250–430 tokens per spoken minute → 32K covers ~1 h of speech plus output room [measured + inference].
- **Ollama truncates silently by default**: HTTP 200, the prompt's *front* is dropped (only 4–5 tokens kept), so the system prompt/title/description are what get lost. Only a server-log WARN shows it [measured, src].
- **OpenRouter** `google/gemma-4-26b-a4b-it`: 262144 ctx; overflow → request error, unless the `context-compression` plugin (middle-out) is on, which is default only for endpoints ≤ 8K [doc, api].
- **Token counting before sending isn't needed** for correctness (the server's 400 is the source of truth). A cheap chars/3 pre-estimate is optional, for logging or to fail fast [inference].

## 2. How long are real transcripts?

Fetched with YoutubeExplode 6.6.2 (first caption track, captions joined with spaces). Tokens = `prompt_eval_count` from Ollama `/api/chat` for one user message `Title/Description/Transcript`, `num_ctx` large enough, `num_predict:1` [measured].

| Video | Length | Track | Transcript chars / words | Prompt chars | **Prompt tokens** | chars/token |
|---|---|---|---|---|---|---|
| `i84Sc5uvQa8` Creamy Garlic Lemon Pasta | 5:28 | en(auto), 358 captions | 7099 / 1362 | 9190 | **2342** | 3.9 |
| `6tMZNYQkycI` 10 Easy Pasta Recipes | 12:52 | en(auto), 45 captions | **255 / 33** | 1274 | **402** | 3.2 |
| `6wR2T-PexT4` Chefkoch Rezepte nachgekocht (DE) | 36:18 | de(auto), 1666 captions | 30994 / 4974 | 31807 | **9200** | 3.5 |

- `6tMZNYQkycI` is **music only**: the transcript is `[Music] … [Applause] … so`. The 10 recipes are only in on-screen text; the description has chapter titles + a link to a printable recipe, no ingredients [measured]. This isn't a length problem. It's a "captions exist but say nothing" case, which will probably end as `NoRecipe` or as invented recipes. Flag it for the prompt/failure tickets [inference].
- German chatty speech: ~255 tokens/min. English cooking narration: ~330 tokens/min of transcript only [measured: 9200/36 min; (7099/3.9)/5.5 min].
- Extrapolated: 60 min ≈ 15–20k, 2 h livestream ≈ 30–50k tokens [inference].
- Output side: ~330 tokens for 2 German recipes in the earlier test [measured, previous note §3] → 10 recipes ≈ 2–4k tokens [inference]. `num_ctx` must hold **input + output**.

## 3. Ollama: default `num_ctx` and overflow

### Default

- The default depends on VRAM: "< 24 GiB: 4k, 24–48 GiB: 32k, ≥ 48 GiB: 256k" [doc https://docs.ollama.com/context-length]. On this Mac the server log says `msg="vram-based default context" total_vram="28.1 GiB" default_num_ctx=32768`, and a request without `num_ctx` loads with `CONTEXT 32768` in `ollama ps` [measured]. A 16 GB Mac would get 4k, which is already too small for the 36-min video [inference from doc].

### What happens on overflow (default: silent truncation)

- `6wR2T-PexT4` (9200 tokens) with `num_ctx:4096` → HTTP 200, `prompt_eval_count:2051`. With `num_ctx:8192` → 200, `4099` [measured]. Server log: `level=WARN msg="truncating input prompt" limit=4099 prompt=9200 keep=5 new=4099` [measured].
- Mechanism [src ollama v0.33.3 `llm/llama_server.go`]: if the tokenized prompt is longer than `NumCtx-1` and context shift is on, it keeps the first `num_keep` tokens (default `NumKeep: 4`, +1 BOS [src `api/types.go`]) and **discards from the front** down to `contextShiftPromptLimit = numCtx - (numCtx-numKeep)/2`, so about **half** of `num_ctx`. Ollama frees half the context so generation has room.
- Consequence: the system prompt, title and description (front of the prompt) are lost first. You can't tell from the 200 response, except that `prompt_eval_count` is lower than expected [measured, inference].
- The chat-level `chatPrompt` also drops whole *older messages* that don't fit, always keeping system messages and the latest message [src `server/prompt.go`]. For our single-turn prompt the runner-level truncation above is what bites [inference].

### Turning truncation into an error

- `ChatRequest` has top-level `truncate *bool` ("truncates the chat history messages if the rendered prompt exceeds the context length limit") and `shift *bool` ("shifts … instead of erroring"). Both default to true when omitted [src `api/types.go`, `server/routes.go`: `req.Truncate == nil || *req.Truncate`].
- Measured with `num_ctx:4096` on the 36-min transcript [measured]:
  - `"truncate": false` → **HTTP 400** `{"error":"{\"error\":{\"code\":400,\"message\":\"request (8889 tokens) exceeds the available context size (4096 tokens), try increasing it\",\"type\":\"exceed_context_size_error\",\"n_prompt_tokens\":8889,\"n_ctx\":4096}}"}`. The real token count comes back for free.
  - `"shift": false` → HTTP 400 `"the prompt is longer than the context length currently available to the model; …"` (no counts).
- **Use `truncate:false`.** It's the clearer error, with `type` and counts [inference].

## 4. Setting `num_ctx` (and `truncate`) from .NET

- `num_ctx` per request: OllamaSharp maps `ChatOptions.AdditionalProperties["num_ctx"]` → `request.Options.NumCtx`. `think` and `keep_alive` work the same way [src OllamaSharp main `MicrosoftAi/AbstractionMapper.cs` `TryAddOllamaOption(… OllamaOption.NumCtx …)`]. Measured: OK [measured].
- `truncate` is **not** a property on OllamaSharp's `ChatRequest` (5.5.0, released 2026-09-30) [src `Models/Chat/ChatRequest.cs`, GitHub releases]. `ChatOptions.RawRepresentationFactory` can supply the `ChatRequest` [src AbstractionMapper line 71], but a subclass with an extra property won't be serialized: `JsonSerializer.Serialize(request, …)` uses the declared `ChatRequest` type [src `OllamaApiClient.cs`; inference about STJ static-type serialization].
- **Works: a `DelegatingHandler` on the `HttpClient` passed to `new OllamaApiClient(httpClient, model)`** that adds `"truncate": false` to `POST /api/chat` JSON bodies. Measured through `IChatClient.GetResponseAsync`: `num_ctx=4096` → `OllamaException` with the JSON above. `num_ctx=16384` → OK, `Usage.InputTokenCount=8881` [measured]. OllamaSharp turns a 400 into an exception, taking the message from the body's `error` field [src `OllamaApiClient.EnsureSuccessStatusCodeAsync`].
  - Gotcha: in a .NET 10 file-based app (`dotnet run x.cs`), OllamaSharp failed with "Reflection-based serialization has been disabled" until `#:property PublishAot=false` [measured]. That doesn't apply to the normal web project [inference].
- The OpenAI-compatible `/v1` path can't set `num_ctx` [doc https://docs.ollama.com/api/openai-compatibility]. That's one more reason for OllamaSharp in the Ollama adapter.
- **Keep `num_ctx` constant.** Each change of `num_ctx` reloaded the model: `load_duration` 3–10 s on every switch, ~0 when unchanged [measured]. A per-request "fit to prompt size" `num_ctx` would pay that on most Imports [inference].

## 5. `gemma4:26b`: max context, memory, latency

- Native context 262144 [measured `/api/show`; doc model card, previous note].
- KV cache (Metal) per `num_ctx`, from server log `llama_kv_cache: MTL0 KV buffer size`. There are two buffers: global-attention layers that scale with ctx, plus a fixed ~300 MiB sliding-window cache [measured]:

| `num_ctx` | scaling KV | + SWA KV | ≈ extra memory |
|---|---|---|---|
| 8192 | 160 MiB | 300 MiB | 0.45 GB |
| 16384 | 320 MiB | 400 MiB | 0.7 GB |
| 32768 | ~640 MiB (interpolated) | 300 MiB | ~0.95 GB |
| 65536 | 1280 MiB | 300 MiB | 1.6 GB |
| 131072 | 2560 MiB | 300 MiB | 2.9 GB |
| 262144 | 5120 MiB | 300 MiB | 5.4 GB |

  ≈ **20 MiB per 1k tokens of context** on top of ~18 GB weights. Fine on 36 GB up to 64K. 256K is possible but steep on a 16–24 GB Mac [measured + inference].
- **Latency doesn't depend on `num_ctx`; it depends on prompt length.** The 9200-token prompt took 18.8–19.9 s prompt-eval at every ctx from 16K to 256K (≈ 465 tok/s). The 2342-token prompt took 4.9 s [measured]. Generation ~40 tok/s → 10 recipes (~3k tokens) ≈ 75 s [inference from previous note's rate].

## 6. OpenRouter

- `google/gemma-4-26b-a4b-it`: `context_length` 262144, top provider `max_completion_tokens` 235929, $0.09/$0.30 per M [api `/api/v1/models`]. The endpoints vary: Darkbloom 131072 ctx, Venice max output 8192, DeepInfra max output 16384. Several endpoints lack `structured_outputs` [api `/models/google/gemma-4-26b-a4b-it/endpoints`]. With `provider.require_parameters:true` (previous note) routing goes to endpoints that support our parameters [inference].
- **Overflow:** "If context compression is disabled and your total tokens exceed the model's context length, the request will fail with an error message suggesting you either reduce the length or enable context compression" [doc https://openrouter.ai/docs/guides/features/message-transforms]. Request errors come back with HTTP status = `error.code` and body `{error:{code,message,metadata?}}` [doc https://openrouter.ai/docs/api-reference/errors]. Not reproduced (no paid calls).
- **Middle-out** is now the `context-compression` plugin (`plugins:[{id:"context-compression"}]`). It removes or truncates content "from the middle of the prompt". It's **on by default only for endpoints with ≤ 8,192 tokens context**. To disable it: `plugins:[{"id":"context-compression","enabled":false}]` [doc same page]. For our model it's off by default. We shouldn't enable it: dropping the middle of a transcript silently drops recipes [inference].
- Practically, with 256K, overflow on OpenRouter needs a ~10+ h video. That's not a real case for us; just map the 400 [inference].

## 7. Cheap token counting in .NET (optional)

- Ollama has **no tokenize endpoint**. The routes in v0.33.3 are generate/chat/embed/show/ps/… only [src `server/routes.go`]. The exact count comes back free in the `truncate:false` 400 (`n_prompt_tokens`) or in `Usage.InputTokenCount` on success [measured].
- `Microsoft.ML.Tokenizers` (2.0.0 stable) has `SentencePieceTokenizer.Create(Stream modelStream, …)` [doc learn.microsoft.com API ref]. Exact Gemma counts would need the Gemma `tokenizer.model` file, which is a gated HF download and a 262k vocab. Not worth it [inference].
- Heuristic: measured 3.2–3.9 chars/token. **`chars / 3`** is a conservative over-estimate for EN/DE [measured]. Use it only for logging or an early fail-fast before a 20 s+ prompt eval [inference].

## 8. Chunking: why not (now)

- The measured worst case (9.2k) is under 30% of a 32K window. Chunking adds real complexity: recipes split across chunk boundaries, a merge/dedupe step, N× prompt cost, and the system prompt repeated per chunk. And it only helps for videos > ~1 h at 32K, or > ~8 h at 256K [inference].
- If needed later, raising `Llm:ContextTokens` (64K costs ~0.7 GB more) is a config change. Chapter-based splitting (description timestamps, see the transcript note §2.4) is the natural chunk seam if it ever comes to that [inference].

## 9. Recommendation for the spec

1. Config `Llm:ContextTokens` (default **32768**), validated on start. The Ollama adapter sends it as `num_ctx` on every request (constant → no reloads). It also matches Ollama's own default on 24–48 GB machines.
2. The Ollama adapter's `HttpClient` gets a `DelegatingHandler` that sets `"truncate": false` on `/api/chat`. This is the guard against silent front-truncation. Unit-test the handler, and in the optional local golden check, assert a 400 with a tiny `num_ctx`.
3. `RecipeExtractor` maps Ollama `OllamaException` whose message contains `exceed_context_size_error` (or a 400 from OpenRouter mentioning context length) → new failure kind **`VideoTooLong`** (non-retryable; German copy like "Video zu lang für die Verarbeitung"). This adds to the kinds in issue 06.
4. Don't enable OpenRouter context compression. Optionally send `context-compression enabled:false` explicitly so routing to a ≤ 8K endpoint can't silently compress.
5. Optional: log `chars/3` estimate vs `Usage.InputTokenCount` per Import.
6. No chunking in V1.

## 10. Open points / risks

- OpenRouter overflow response not reproduced (no paid calls). Exact status and message text unverified, so match loosely.
- The handler that rewrites the JSON body is coupled to OllamaSharp's wire format. Re-check on OllamaSharp upgrades, or upstream a `Truncate` property (small PR) [inference].
- Music-only / non-verbal videos (`6tMZNYQkycI`) yield near-empty transcripts. That's a separate failure path (`NoRecipe`, or should it be `NoCaptions`?), not a context issue.
- Auto-caption ASR noise (Russian hallucinated opening lines in `6wR2T-PexT4`) costs a few tokens. It doesn't matter for length.
