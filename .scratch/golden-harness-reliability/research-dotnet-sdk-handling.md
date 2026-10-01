# Research: .NET SDK handling of OpenRouter finish_reason "error" and strict JSON schema

Pinned: Microsoft.Extensions.AI 10.10.0, Microsoft.Extensions.AI.OpenAI 10.10.1 (`backend/Directory.Packages.props:11-12`), which depends on OpenAI 2.14.0 (`~/.nuget/packages/microsoft.extensions.ai.openai/10.10.1/*.nuspec:20`). OpenAI 2.14.0 is the newest tag/release in openai/openai-dotnet (`gh api repos/openai/openai-dotnet/releases` -> latest OpenAI_2.14.0).

Everything below marked "PROBED" was reproduced with a scratch console project against the exact pinned packages (stub HttpMessageHandler as transport). Source: `/private/tmp/claude-503/-Users-olst-gitroot-recipe-app/ed6f5550-6398-4112-b7c3-9ebb98e8087e/scratchpad/probe/Program.cs` (not in repo).

## Summary: ranked recommendations

1. **Failure 1: add a `DelegatingHandler` on the HttpClient used as transport that rewrites `"finish_reason":"error"` to a value the SDK knows (e.g. `"stop"`), after surfacing the error.** Upgrading will not help (see Q1). PROBED: works for non-streaming. A `PipelinePolicy` cannot do it simply, because `PipelineResponse.Content` is read-only (compile error CS0200 when tried); a policy would have to swap in a custom `PipelineResponse` subclass. Better: have the handler detect `finish_reason=="error"` / `choices[].error` and map to a distinct failure (don't pretend "stop"; a truncated reply then fails JSON parse or yields BadOutput anyway). Our only call is non-streaming (`GetResponseAsync`), so streaming is optional, but see Q1 for the streaming parse site.
2. **Failure 1 alternative: catch `ArgumentOutOfRangeException` whose message contains "Unknown ChatFinishReason" in RecipeExtractor and map it to a retryable/provider-error failure.** Cheapest, but loses the OpenRouter error body (which tells why). Combine with #1's logging if wanted.
3. **Failure 3/Q3 bug in our workaround: the override does take effect but ships a WEAKER schema.** Replace the `raw.ResponseFormat = CreateJsonSchemaFormat(...)` block with `options.AdditionalProperties["strict"] = true` (MEAI then emits `strict:true` itself AND applies its strict transform: `additionalProperties:false`, all properties in `required`). PROBED.
4. **Failure 2: the schema allows `null` because the DTO is nullable.** `IReadOnlyList<string>?` becomes `"type":["array","null"]`. Make `IngredientLines`/`Steps` non-nullable (`IReadOnlyList<string>`); schema then says `"type":"array"`. PROBED. Optionally add `[Description]` to guide the model (it is emitted; PROBED). Keep a null-tolerant fallback in `Clean` regardless (providers that ignore strict).
5. Prove on the wire with the existing stub-endpoint test, extended (see Q3).

## Q1: ChatFinishReason parsing in OpenAI 2.14.0

- Closed enum: `public enum ChatFinishReason { Stop, Length, ToolCalls, ContentFilter, FunctionCall }` - https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/OpenAI/src/Custom/Chat/ChatFinishReason.cs (enum at line 43; FunctionCall line 88).
- Parser: `ChatFinishReasonExtensions.ToChatFinishReason(string)` compares case-insensitively with stop/length/tool_calls/content_filter/function_call, else `throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown ChatFinishReason value.")` - https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/OpenAI/src/Generated/Models/Chat/ChatFinishReason.Serialization.cs lines 21-44 (throw at line 43). Auto-generated file.
- Call sites:
  - Non-streaming: `InternalCreateChatCompletionResponseChoice.DeserializeInternalCreateChatCompletionResponseChoice`, `finishReason = prop.Value.GetString().ToChatFinishReason();` at line 134 of https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/OpenAI/src/Generated/Models/Chat/InternalCreateChatCompletionResponseChoice.Serialization.cs
  - Streaming: line 101 of https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/OpenAI/src/Custom/Chat/Streaming/InternalCreateChatCompletionStreamResponseChoice.Serialization.cs (exception then surfaces from the async enumerator).
- Not fixed in newer versions: `main` still has the same throwing switch (https://raw.githubusercontent.com/openai/openai-dotnet/main/OpenAI/src/Generated/Models/Chat/ChatFinishReason.Serialization.cs, fetched today; lines 21+ identical). No newer release than 2.14.0 exists. Issue #340 "custom finish_reason support" (closed): maintainer jsquire: library is intended to be compatible with OpenAI and its service contract; other services not API-supported - https://github.com/openai/openai-dotnet/issues/340. Related report for null finish_reason: https://github.com/openai/openai-dotnet/issues/342 (closed, no fix seen; UNVERIFIED whether any change was merged). Conclusion: upgrading does not fix; a workaround is required.
- MEAI side would handle unknown values (`FromOpenAIFinishReason` default arm `new ChatFinishReason(s)`, https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L832-L840) but never gets the chance, because the SDK throws first.
- PROBED repro: stub returning `finish_reason:"error"` -> `ArgumentOutOfRangeException: Unknown ChatFinishReason value. (Parameter 'value') Actual value was error.` Same message as in production.
- OpenRouter's behaviour (HTTP 200 with `finish_reason:"error"` and a `choices[].error` object): UNVERIFIED here (not fetched; see https://openrouter.ai/docs/api-reference/errors).

### Minimal sketch (handler on the transport; non-streaming PROBED to turn the exception into `finish=stop`)

```csharp
sealed class FinishReasonFix(HttpMessageHandler inner) : DelegatingHandler(inner)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
    {
        var resp = await base.SendAsync(r, ct);
        if (!resp.IsSuccessStatusCode || resp.Content.Headers.ContentType?.MediaType != "application/json") return resp;
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!json.Contains("\"finish_reason\"")) return resp;
        // TODO: log choices[].error here, or throw HttpRequestException so it maps to LlmUnavailable with a useful message.
        var fixedJson = Regex.Replace(json, "\"finish_reason\"\\s*:\\s*\"error\"", "\"finish_reason\":\"stop\"");
        resp.Content = new StringContent(fixedJson, Encoding.UTF8, "application/json");
        return resp;
    }
}

// in CreateOpenRouter:
new OpenAIClientOptions {
    Endpoint = ..., RetryPolicy = new ClientRetryPolicy(0), NetworkTimeout = ...,
    Transport = new HttpClientPipelineTransport(new HttpClient(new FinishReasonFix(new HttpClientHandler()))),
}
```

Notes:
- `HttpClientPipelineTransport` ctor taking an HttpClient: used in the probe, compiles against System.ClientModel as pinned. Set `HttpClient.Timeout` appropriately (the SDK's `NetworkTimeout` is applied by the pipeline; the HttpClient default of 100 s would otherwise cut long generations; set `Timeout = Timeout.InfiniteTimeSpan`). That last detail is UNVERIFIED (not probed).
- Streaming: SSE chunks are `data: {...}` lines; the same regex works per chunk but requires a streaming content wrapper (do not buffer with `ReadAsStringAsync`). We do not stream, so skip it.
- Alternatives: `PipelinePolicy` via `OpenAIClientOptions.AddPolicy` (reads response after `ProcessNext`) cannot assign `Response.Content` (read-only, probe compile error). A custom `IChatClient` is overkill.

## Q2: Schema generation in `GetResponseAsync<T>(useJsonSchemaResponseFormat: true)` (v10.10.1)

Flow (all https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/...):
- `Microsoft.Extensions.AI/ChatCompletion/ChatClientStructuredOutputExtensions.cs:151` calls `ChatResponseFormat.ForJsonSchema<T>(serializerOptions)`; lines 155-171 wrap non-object roots in `{data: ...}`; lines 174-181 clone options and set `options.ResponseFormat = responseFormat`.
- `Microsoft.Extensions.AI.Abstractions/ChatCompletion/ChatResponseFormat.cs` (ForJsonSchema ~lines 79-95; `_inferenceOptions = new() { IncludeSchemaKeyword = true }` at line 22-25): schema from `AIJsonUtilities.CreateJsonSchema(type, serializerOptions, inferenceOptions)`; schema name = type name (non-alnum -> `_`), description from `[Description]` on the type.
- `Microsoft.Extensions.AI.Abstractions/Utilities/AIJsonUtilities.Schema.Create.cs` (lines 190-300): uses System.Text.Json `JsonSchemaExporter` with `TreatNullObliviousAsNonNullable = true`; per-node `TransformSchemaNode` adds `[Description]` from the property/parameter (`ctx.GetCustomAttribute<DescriptionAttribute>()`, line ~283, inserted at start of node, line ~385), plus `[Required]`, ranges, etc.
- **`strict` is NOT set by the generic layer.** In the OpenAI adapter, `ToOpenAIChatResponseFormat` (https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L689-L703) passes `OpenAIClientExtensions.HasStrict(options?.AdditionalProperties)` as `jsonSchemaIsStrict`; `HasStrict` reads `AdditionalProperties["strict"]` (bool) (OpenAIClientExtensions.cs:33, 202-205). So `strict` is only emitted when `ChatOptions.AdditionalProperties["strict"] = true`. PROBED: with it, body has `"strict":true`; without it, no `strict` key.
- **The OpenAI adapter always applies its strict transform to the schema** (`StrictSchemaTransformCache`, OpenAIClientExtensions.cs:48-110): `DisallowAdditionalProperties = true`, `ConvertBooleanSchemas`, `MoveDefaultKeywordToDescription`, `RequireAllProperties = true`, and moves unsupported keywords (minLength, pattern, format, minimum, minItems, etc.) into `description`. PROBED: even without `strict`, body has `additionalProperties:false` on every object and every property in `required`.
- Nullable representations (PROBED, our DTO): `string?` -> `"type":["string","null"]`; `int?` -> `["integer","null"]`; `IReadOnlyList<string>?` -> `{"type":["array","null"],"items":{"type":"string"}}`. Type arrays, not anyOf. Non-nullable `IReadOnlyList<string>` -> `"type":"array"`. All five properties are in `required` (RequireAllProperties). `$schema` is included (`IncludeSchemaKeyword`).
- Descriptions: `[Description("...")]` on a record parameter needs `[property: Description(...)]`; PROBED: emitted as `"description"` inside the property schema (`"description":"Ingredient lines, never null"`).
- Configurability: `AIJsonSchemaCreateOptions` has `TransformSchemaNode`, `IncludeParameter`, `ParameterDescriptionProvider`, `TransformOptions` (`AIJsonSchemaTransformOptions`), `IncludeSchemaKeyword` (https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/Microsoft.Extensions.AI.Abstractions/Utilities/AIJsonSchemaCreateOptions.cs#L25-L59). `DisallowAdditionalProperties` / `RequireAllProperties` live in `AIJsonSchemaTransformOptions` (reached via `TransformOptions`), not on the create options directly (UNVERIFIED for exact member list; seen used at OpenAIClientExtensions.cs:50-53). **But** `ChatResponseFormat.ForJsonSchema<T>` hardcodes `_inferenceOptions`, so the create options are not configurable through the `GetResponseAsync<T>`/`ForJsonSchema<T>` overloads; only `JsonSerializerOptions` is. To control the schema fully, call `AIJsonUtilities.CreateJsonSchema(typeof(T), serializerOptions: ..., inferenceOptions: new() {...})` yourself and use `ChatResponseFormat.ForJsonSchema(JsonElement schema, name, description)` with `GetResponseAsync(messages, options)` and parse the text manually (the generic `GetResponseAsync<T>` always overwrites `options.ResponseFormat`, SO.cs:179-181).
- Serializer options: `AIJsonUtilities.DefaultOptions` is `JsonSerializerDefaults.Web` (camelCase) with `DefaultIgnoreCondition = WhenWritingNull` (https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/Microsoft.Extensions.AI.Abstractions/Utilities/AIJsonUtilities.Defaults.cs#L64-L66; UNVERIFIED which of the two matches the runtime default instance, line 149 has the same attribute).

## Q3: Does the LlmClientFactory workaround take effect?

- Yes, it takes effect, but it is harmful as written. `ToOpenAIOptions` (https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/Microsoft.Extensions.AI.OpenAI/OpenAIChatClient.cs#L601-L686): line 608 calls `options.RawRepresentationFactory?.Invoke(this)` and uses the returned `ChatCompletionOptions` as the base object; line 684: `result.ResponseFormat ??= ToOpenAIChatResponseFormat(options.ResponseFormat, options);`. `??=` means MEAI only fills `ResponseFormat` if the raw one left it null, so our value wins and the MEAI strict transform (inside `ToOpenAIChatResponseFormat`) is bypassed.
- Consequence (PROBED, wire body with the override): `"strict":true` present, `provider.require_parameters:true` present, but the schema is the RAW one: **no `additionalProperties:false`**, same `required` list. OpenAI-style strict mode requires `additionalProperties:false` on every object (https://platform.openai.com/docs/guides/structured-outputs#supported-schemas, cited from the comment in OpenAIClientExtensions.cs:45-46), so some providers may reject it or silently downgrade. Without the override MEAI sent the transformed schema with `additionalProperties:false` but no `strict`.
- `RawRepresentationFactory` on the cloned options: `ChatOptions.Clone()` copies `RawRepresentationFactory` (https://github.com/dotnet/extensions/blob/v10.10.1/src/Libraries/Microsoft.Extensions.AI.Abstractions/ChatCompletion/ChatOptions.cs#L37). Our wrapper reads `options.ResponseFormat` inside the lambda from the clone; `GetResponseAsync<T>` has already set it (SO.cs:179-181) before the call reaches our `DelegatingChatClient`, so the `is ChatResponseFormatJson { Schema: ... }` check matches (PROBED: strict true appeared). The factory runs per call in both non-streaming (`OpenAIChatClient.cs:98`) and streaming (`:119`) paths.
- `Patch.Set("$.provider", ...)`: survives. `ToOpenAIOptions` only mutates the returned object (adds model via `PatchModelIfNotSet(ref result.Patch, options.ModelId)`, line ~625) and returns it. PROBED: body ends with `"provider":{"require_parameters":true}`.
- Recommended replacement: drop the `raw.ResponseFormat` block and set in the wrapper `options.AdditionalProperties ??= new(); options.AdditionalProperties["strict"] = true;`. PROBED result: `"strict":true` AND `additionalProperties:false` AND all `required`, with non-nullable lists `"type":"array"`.
- How to prove on the wire: `backend/tests/RecipeJoe.UnitTests/Video/LlmClientFactoryTests.cs` test `OpenRouter_requests_a_strict_json_schema_from_providers_that_honour_it` (lines ~33-56) already starts an `HttpListener` stub, captures the request body, and asserts: `response_format.type == "json_schema"`, `response_format.json_schema.strict == true`, `provider.require_parameters == true`. It does NOT assert anything about the schema content. Extend with: every object node in `json_schema.schema` has `additionalProperties == false`; `required` contains all property names; `ingredientLines`/`steps` `type` is not an array containing `"null"` (after making the DTO non-nullable). Add a second stub test where the reply has `"finish_reason":"error"` and assert the extractor's chosen failure (after adding the handler fix). Note `ExtractionReply` is in the test (internal visibility works since the test already uses it).
- Golden harness can also log the body via the same `DelegatingHandler` (log `request.Content.ReadAsStringAsync()` before `base.SendAsync`).
