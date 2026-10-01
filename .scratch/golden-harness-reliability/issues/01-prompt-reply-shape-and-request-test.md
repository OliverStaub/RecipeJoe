# 01: Prompt reply shape + request-shape test

**Spec:** [../spec.md](../spec.md)

**What to build:** The prompt states the exact reply shape, the strict-schema and routing request settings are tested, and golden is verified stable.

**Status:** ready-for-agent

- [ ] Prompt: empty case is literally `{"recipes": []}` (no "leere Liste")
- [ ] Prompt: minimal JSON example, ingredient lines and steps as plain strings
- [ ] Request sends the JSON-schema response format with `strict: true` (fix if not)
- [ ] Unit test (client factory seam, local stub endpoint): outgoing request has strict JSON-schema response format and `provider.require_parameters = true`
- [ ] Extractor stays strict; bare-array reply covered as `LlmBadOutput` if not already
- [ ] `just golden` run at least 3 times; per-video results (0 / 1 / 5 recipes) noted in Comments
- [ ] If still flaky: stronger model via `Llm__Model` or pinned provider; decision noted in Comments

## Comments
