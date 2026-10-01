# Research: LLM providers for Video Import (Ollama local Gemma + OpenRouter)

Date: 2026-09-30. Scope: turning video text (transcript, title, description, chapters) into structured Recipe JSON from the .NET 10 backend. Covers reaching host Ollama from the compose backend, Gemma variants for a Mac, structured output in both providers, OpenAI compatibility, .NET client options, OpenRouter pricing and keys, and timeouts. Ends with a recommended adapter shape. Ticket: `.scratch/video-import/issues/02-llm-providers-ollama-openrouter.md`.

Legend: **[doc]** = stated in the primary source linked. **[src]** = read in upstream source code. **[api]** = read from a live first-party API response on 2026-09-30. **[measured]** = run locally for this note (Apple M3 Pro, 36 GB, Ollama 0.33.3, `gemma4:26b`, Docker via Colima 0.10.3). **[inference]** = my conclusion, not stated anywhere.

---

## 1. What's installed locally

- `ollama list`: **`gemma4:26b`** (ID `08ae7ec1744b`, 18 GB on disk). `/api/version`: `0.33.3` [measured].
- `/api/show`: family `gemma4`, 25.2B params, `Q4_K_M` quantisation, 128 experts with 8 active, native context length 262144. Capabilities: `completion, vision, tools, thinking` [measured].
- Ollama listens on `127.0.0.1:11434` only. `OLLAMA_HOST` is not set [measured].
- Docker runs on **Colima** (the active context), not Docker Desktop [measured]. This affects §2.
- Oddity: `ollama ps` shows SIZE 1.2 to 1.4 GB for a model that is 18 GB on disk. That's probably a reporting quirk with mmap on Apple Silicon. Don't use that column for memory planning [inference].

## 2. Reaching host Ollama from the compose backend

- By default Ollama binds to `127.0.0.1:11434`. To expose it on the network, set `OLLAMA_HOST`. On macOS: `launchctl setenv OLLAMA_HOST "0.0.0.0:11434"`, then restart the app [doc https://docs.ollama.com/faq].
- **Measured on this Mac (Colima):** from a container built from the existing `recipe-app-backend` image, `curl http://host.docker.internal:11434/api/version` returned `{"version":"0.33.3"}`. Ollama was still bound to 127.0.0.1. `host.lima.internal` also works. With `--add-host=host.docker.internal:host-gateway`, the name resolves to `192.168.5.2` (the Lima host gateway) and that works too [measured]. So **no `OLLAMA_HOST` change is needed**. The VM's user-mode network forwards the gateway address to host loopback [inference].
- **Recommendation:** add `extra_hosts: ["host.docker.internal:host-gateway"]` to the `backend` service and set the base URL to `http://host.docker.internal:11434`. This makes the name explicit and portable across Colima, Docker Desktop and Linux engines [inference; host-gateway measured on Colima]. Don't bind to `0.0.0.0`: Ollama has no auth, so that would expose the model to the LAN [inference].
- When the backend runs outside compose (`dotnet run`), the base URL is `http://localhost:11434`. That's a config value, not code.
- Ollama inside Docker on macOS gets **no GPU**: "macOS Docker doesn't support GPU passthrough" [doc faq]. Keep Ollama on the host.

## 3. Gemma variants for a Mac

Latest family: **Gemma 4**, released 2 April 2026 under Apache 2.0 [doc https://blog.google/innovation-and-ai/technology/developers-tools/gemma-4/]. OpenRouter lists `google/gemma-4-26b-a4b-it` as created 2026-04-03 [api]. Neither the Ollama library nor OpenRouter lists anything newer than Gemma 4 [api, doc]. Supports 35+ languages out of the box and was pre-trained on 140+ [doc https://ai.google.dev/gemma/docs/core/model_card_4].

| Variant (Ollama tag) | Params | Context | Ollama download | Fits |
|---|---|---|---|---|
| `gemma4:e2b` | 2.3B effective | 128K | 4.6 to 7.5 GB | any Mac, weakest |
| `gemma4:e4b` (= `latest`) | 4.5B effective | 128K | 6.6 to 9.5 GB | 16 GB Macs |
| `gemma4:12b` | 11.95B dense | 256K | 7.7 to 8.0 GB | 16 to 24 GB Macs |
| **`gemma4:26b`** (installed) | 25.2B total / **3.8B active** (MoE) | 256K | 16 to 19 GB | 32 GB+ Macs |
| `gemma4:31b` | 30.7B dense | 256K | 19 to 20 GB | 32 GB+, slower |

Sources: sizes and tags [doc https://ollama.com/library/gemma4], params and context [doc model card]. `-mlx` tags exist for Apple Silicon [doc ollama library]; they weren't tried.

**Speed of `gemma4:26b` on the M3 Pro** [measured, `/api/chat`, `think:false`, temperature 0]:

- Cold load: 12.4 s. The model unloads after 5 minutes idle by default; `OLLAMA_KEEP_ALIVE` or per-request `keep_alive` changes that [doc faq].
- Small prompt (126 tokens in, 310 out): 19.8 s total with the cold load, 14.9 s warm. Generation ran at **47 tok/s**.
- Large prompt (5318 tokens in, 328 out): prompt processing at **468 tok/s** (11.4 s), generation at **40 tok/s**. Total 19.6 s.
- Rough estimate for a 30-minute video (~8k tokens of text in, ~2k tokens of German recipes out): about 17 s of prompt processing, 50 s of generation, plus 12 s if the model is cold. That's **roughly 60 to 80 s** [inference from the measured rates].
- Output quality on a toy English transcript containing 2 recipes: it returned both, in correct German, and matched the schema [measured]. `servings` came back `null` because the transcript didn't mention it, which is correct.
- Why it's fast: the MoE runs only about 4B parameters per token, so it generates at roughly small-model speed while having 26B-model knowledge [inference]. The dense `31b` would be several times slower [inference, not measured].

### Context window: a trap

- Ollama doesn't default to the model's full 256K. The default depends on VRAM: "< 24 GiB VRAM: 4k", "24-48 GiB: 32k", ">= 48 GiB: 256k" [doc https://docs.ollama.com/context-length]. To override, set `OLLAMA_CONTEXT_LENGTH` on the server, or `num_ctx` per request on the native API [doc].
- On this Mac a `/v1` request got **32768** (checked with `ollama ps`) [measured]. On a 16 GB Mac it would get 4k.
- "The OpenAI API does not have a way of setting the context size for a model." Use a Modelfile `PARAMETER num_ctx` or the server environment variable instead [doc https://docs.ollama.com/api/openai-compatibility].
- An input longer than `num_ctx` gets truncated by Ollama, not rejected [inference; known behaviour, not found stated on the pages read]. **Guard:** compare the response's `prompt_eval_count` (native API) or `usage.prompt_tokens` (`/v1`) with what you expected. This matters for the "long videos vs context" open question on the map.

## 4. Structured output

**Ollama**

- Native `/api/chat`: `format` takes `"json"` or a full JSON Schema object. The docs advise: "It is ideal to also pass the JSON schema as a string in the prompt to ground the model's response". They also recommend temperature 0 [doc https://docs.ollama.com/capabilities/structured-outputs].
- OpenAI-compatible `/v1/chat/completions`: "Structured outputs work through the OpenAI-compatible API via `response_format`" [doc structured-outputs]. Supported fields include `response_format`, `tools`, `reasoning_effort`, `seed` and `stream_options`. `tool_choice`, `logit_bias`, `n` and `logprobs` are not supported [doc openai-compatibility].
- Measured: `response_format: {type:"json_schema", json_schema:{name, strict:true, schema}}` plus `reasoning_effort:"none"` on `/v1` returned the same schema-valid JSON as the native `format`. No reasoning field was present [measured].
- Gemma 4 can "think". Turn it off for extraction (`think:false` native, `reasoning_effort:"none"` on `/v1`) to save tokens and time [measured that both switches work; the time saving is inference].

**OpenRouter**

- Same OpenAI shape: `response_format` `{type:"json_schema", json_schema:{name, strict, schema}}`. But "enforcement varies by provider: some guarantee schema-conforming output, while others translate your schema". Support is per provider endpoint, not per model. To route only to endpoints that support it, set `provider.require_parameters: true`. For non-streaming JSON there's a "Response Healing" plugin [doc https://openrouter.ai/docs/features/structured-outputs].
- For `google/gemma-4-26b-a4b-it`, 14 endpoints are listed. 9 advertise `structured_outputs`; DekaLLM, Makora, Cloudflare, Novita and Io Net do not [api `/api/v1/models/google/gemma-4-26b-a4b-it/endpoints`]. **The `:free` variant has no `structured_outputs`** [api].

**In both cases:** validate the JSON server-side anyway. Schema-constrained output guarantees the *shape*, not a *usable Recipe* (empty steps, invented quantities). That's the "LLM output validation" open item [inference].

## 5. One OpenAI-compatible API?

Yes, both speak OpenAI Chat Completions:

- Ollama: base `http://<host>:11434/v1/`. "An API key value is required but ignored" [doc openai-compatibility].
- OpenRouter: base `https://openrouter.ai/api/v1`, `Authorization: Bearer <OPENROUTER_API_KEY>`. The attribution headers `HTTP-Referer` and `X-OpenRouter-Title` are optional. The OpenAI SDK works when you point `baseURL` at it [doc https://openrouter.ai/docs/quickstart].

So **one adapter plus a base URL is technically enough**. What you lose on Ollama with `/v1` [doc openai-compatibility; inference]:

- no per-request `num_ctx` (it must be set on the host, see §3)
- no per-request `keep_alive`
- native timing fields (`load_duration`, `eval_duration`) aren't returned (`usage` token counts are)

What you lose on OpenRouter with a plain OpenAI client: OpenRouter-only body fields like `provider.require_parameters` need an escape hatch. On this path that's `ChatOptions.RawRepresentationFactory` / `AdditionalProperties` [doc ichatclient]. Whether `Microsoft.Extensions.AI.OpenAI` forwards unknown top-level fields like `provider` is **untested** [inference].

## 6. .NET client options

Current NuGet versions [api nuget.org]: `Microsoft.Extensions.AI` **10.10.0**, `Microsoft.Extensions.AI.OpenAI` 10.10.1, `OpenAI` 2.14.0, `OllamaSharp` 5.5.0. `Microsoft.Extensions.AI.Ollama` is **deprecated**: "the OllamaSharp package is recommended" [api nuget deprecation metadata].

- **Microsoft.Extensions.AI (MEAI):** `IChatClient` is the provider-neutral abstraction. It has middleware (`UseLogging`, `UseOpenTelemetry`, `UseDistributedCache`, custom `DelegatingChatClient`) and DI registration via `AddChatClient(...)`. Keyed pipelines are possible [doc https://learn.microsoft.com/en-us/dotnet/ai/ichatclient]. Structured output: `chatClient.GetResponseAsync<T>(prompt)` derives a JSON schema from `T` and returns `response.Result` [doc https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/structured-output].
  - OllamaSharp's `OllamaApiClient` implements `IChatClient` [doc ichatclient]. It maps `ChatOptions.ResponseFormat` (JSON schema) to Ollama `format`, and `Reasoning.Effort = None` to `think:false`. It passes Ollama options such as `num_ctx` through `ChatOptions.AdditionalProperties` [src `OllamaSharp/MicrosoftAi/AbstractionMapper.cs`].
  - The OpenAI SDK's `ChatClient(...).AsIChatClient()` (from `Microsoft.Extensions.AI.OpenAI`) works for both OpenRouter and Ollama `/v1` [doc structured-output quickstart; doc openai-dotnet README].
- **OpenAI .NET SDK directly:** custom `Endpoint` through `OpenAIClientOptions`, plus `ApiKeyCredential`. It retries up to 3 times with exponential backoff on 408/429/500/502/503/504. Structured output via `ChatResponseFormat.CreateJsonSchemaFormat` [doc https://github.com/openai/openai-dotnet]. **The default network timeout is 100 s** (`ClientPipeline.DefaultNetworkTimeout = TimeSpan.FromSeconds(100)`), set through `OpenAIClientOptions.NetworkTimeout` [src azure-sdk-for-net `System.ClientModel/src/Pipeline/ClientPipeline.cs`].
- **Raw `HttpClient`:** the least magic, and it matches the existing `HttpFetcher` style. But you'd hand-write request and response DTOs twice, plus schema generation. Not worth it when MEAI covers both providers [inference].

## 7. OpenRouter: price and key handling

Prices per million tokens, input / output [api `https://openrouter.ai/api/v1/models`, 2026-09-30]:

| Model | In | Out | Context | structured_outputs |
|---|---|---|---|---|
| `google/gemma-4-26b-a4b-it` (same model as local) | $0.09 | $0.30 | 262K | yes (9 of 14 endpoints) |
| `google/gemma-4-31b-it` | $0.09 | $0.34 | 262K | yes |
| `google/gemma-4-26b-a4b-it:free` | 0 | 0 | 262K | **no** |
| `google/gemini-2.5-flash-lite` | $0.10 | $0.40 | 1M | yes |
| `openai/gpt-5-nano` | $0.05 | $0.40 | 400K | yes |

- **Cost per Video Import** at ~10k input + 2k output tokens on Gemma 4 26B: 10k × $0.09/M + 2k × $0.30/M ≈ **$0.0015**, so about 650 Imports per dollar [inference from list prices]. Individual endpoints vary from $0.042/$0.22 to $0.15/$0.60 [api endpoints].
- **Recommendation:** default to `google/gemma-4-26b-a4b-it`, the same model as local. Then the golden check against local Ollama also says something about OpenRouter output [inference].
- Free variants are limited to 20 requests/min and 50/day (1000/day once $10 of credits have been bought). The API returns 429 with `Retry-After`, and 402 when credits run out [doc https://openrouter.ai/docs/api-reference/limits]. They also lack structured outputs, so don't use `:free`.
- **Keys:** Bearer token. Put a credit limit on every key: "A key with no limit lets a leaked key or a runaway agent spend your entire balance, including any auto top-ups". Keys come from environment variables, never from the repo or client code. OpenRouter takes part in GitHub secret scanning and emails on exposure [doc https://openrouter.ai/docs/api-reference/authentication].
- **Local key handling:** use `dotnet user-secrets` for `dotnet run`, and a git-ignored `.env` passed to compose (e.g. `OpenRouter__ApiKey`). This matches how `POSTGRES_PASSWORD` already flows [inference]. CI never needs the key (fake provider).

## 8. Timeouts for long generations

- Expect up to about 80 s locally for a long video, more on a smaller Mac or if the model is cold (§3) [inference].
- This collides with two 100 s defaults: the OpenAI SDK's 100 s network timeout [src], and `HttpClient.Timeout` (also 100 s by default in .NET) [inference; standard .NET default].
- **Recommendation:** follow the existing `ImportOptions.Timeout` / `HttpFetcher` pattern already in the repo. That means an infinite transport timeout plus a per-call `CancellationTokenSource.CancelAfter(...)` from config, e.g. `Llm:Timeout = 00:05:00` [inference; pattern read in `backend/src/RecipeJoe.Api/Import/HttpFetcher.cs`].
- Imports run in the background, so a long wait costs nothing in the UI.
- Set `OpenAIClientOptions.NetworkTimeout` to the same value, or longer, so the SDK doesn't cut the call at 100 s first.
- Also: turn off SDK retries for Ollama (a retry of an 80 s call doubles the wait) or cap them. Treat 429/402 from OpenRouter as a failed Import with a clear message [inference].
- `OLLAMA_NUM_PARALLEL` defaults to 1, and memory scales with it [doc faq]. With several pending Imports, Ollama queues them, so a pending Import may wait for the one ahead. Either the timeout has to allow for queueing, or the backend serialises LLM calls itself [inference].

## 9. Recommended adapter shape

**[inference, all of this section]**

1. **Domain seam above the LLM.** Something like `IRecipeExtractor.ExtractAsync(VideoText, ct) → IReadOnlyList<RecipeDraft>`. One deep module owns the prompt, German style guide, schema, JSON validation and the timeout. API and E2E tests fake *this*, per the standing decision "fake provider in unit/API/E2E".
2. **Provider seam = MEAI `IChatClient`.** It's the standard .NET abstraction, it's DI-friendly, and it already gives `GetResponseAsync<T>` structured output. Unit tests of the extractor use a hand-written fake `IChatClient` returning canned JSON, including malformed JSON.
3. **Two thin config-selected factories, not two adapters to maintain:**
   - `Llm:Provider=Ollama` → `new OllamaApiClient(BaseUrl, Model)` (OllamaSharp). This uses the native API, so `num_ctx`, `think:false` and `keep_alive` go per request. It doesn't depend on the host's `OLLAMA_CONTEXT_LENGTH` and is safe on a 16 GB Mac.
   - `Llm:Provider=OpenRouter` → `new OpenAI.Chat.ChatClient(Model, new ApiKeyCredential(key), new OpenAIClientOptions { Endpoint = https://openrouter.ai/api/v1, NetworkTimeout = ... }).AsIChatClient()`.
   - Each is about 5 to 10 lines of registration. Everything above them is shared.
4. **Simpler fallback:** OpenAI SDK for both (Ollama at `/v1`). That's one package pair and one code path, and it's proven to work here. The cost is a documented `OLLAMA_CONTEXT_LENGTH` on the host plus a truncation check on `usage.prompt_tokens`. It's acceptable if you'd rather have fewer dependencies than per-request control.
5. **Config:** `Llm:Provider`, `Llm:BaseUrl`, `Llm:Model`, `Llm:Timeout`, `Llm:ApiKey` (OpenRouter only, from secrets). Compose sets `Llm__BaseUrl=http://host.docker.internal:11434` plus `extra_hosts`.

## 10. Risks and open points

- **Silent context truncation** on Ollama if `num_ctx` is too small (4k default on under-24 GB machines). Mitigate with per-request `num_ctx` and a token-count check.
- **OpenRouter provider variance:** not every endpoint enforces the schema. Use `provider.require_parameters: true`. Whether MEAI/OpenAI SDK can send that field through is untested; verify it in the first slice or pin a provider.
- **100 s default timeouts** in the OpenAI SDK and `HttpClient` are shorter than a long local generation.
- **Ollama queueing** (`OLLAMA_NUM_PARALLEL=1`) when several Imports run at once.
- **Memory:** `gemma4:26b` at 18 GB is fine on 36 GB, but it competes with Colima (2 GB VM) plus IDE and browser. A 16 GB Mac should use `gemma4:e4b` or `12b`. Make the model a config value.
- Schema-valid ≠ good Recipe. Server-side validation is still needed (map open item).
- MEAI's `GetResponseAsync<T>` generates the schema from `T`, and that schema has its own conventions (nullable handling, `additionalProperties`). OllamaSharp's mapper applies `DisallowAdditionalProperties = true`, though only to tool schemas [src]. Check the schema sent for the Recipe DTO in the first slice [inference].
- Measurements come from one toy transcript. Real German-quality judgement belongs to the golden-corpus work.
