# 02: Surface provider errors

**Spec:** [../spec.md](../spec.md) · **Research:** [../research-dotnet-sdk-handling.md](../research-dotnet-sdk-handling.md), [../research-openrouter-structured-outputs.md](../research-openrouter-structured-outputs.md)

**What to build:** When OpenRouter reports a provider failure (`finish_reason: "error"`, HTTP 200, or a top-level error object), the Import fails with a log line carrying the provider's real message, instead of an opaque SDK enum exception reported as `LlmUnavailable` with no cause. A golden failure then says which provider failed and why.

**Blocked by:** None (can start immediately)

**Status:** done (golden re-run pending: needs Llm__ApiKey)

- [x] Provider-error replies are detected at the HTTP transport (the OpenAI SDK's finish-reason enum is closed and a pipeline policy cannot replace the response body; research notes why)
- [x] The extractor logs the provider's error message and, when present, the provider name
- [x] Failure kind stays `LlmUnavailable` (provider failure), but with the real cause in the log
- [x] Unit test (stub endpoint): a reply with `finish_reason: "error"` and one with a top-level error object each end as `LlmUnavailable` with the provider message logged
- [x] Golden test output shows the cause for such a failure
- [ ] Verify: re-run `just golden`; note whether the two earlier `error` failures now show a readable cause (likely cause: see ticket 03)

## Comments
