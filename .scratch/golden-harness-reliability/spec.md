Status: ready-for-agent

# Golden harness reliability

## Problem Statement

`just golden` runs the recorded videos through the real Video Import extraction (transcript → OpenRouter LLM → extractor). On the last runs, two of three videos failed with `LlmBadOutput` while the model understood the videos fine:

- Video with no Recipe: the model replied with a bare `[]` instead of `{"recipes": []}`. The prompt says "gib eine leere Liste zurück" and the model took it literally.
- Video with 5 Recipes: all 5 were extracted in good German, but steps came back as `[{"text": "..."}]` instead of plain strings, so the reply didn't match the output schema.

Results also vary run to run. A golden failure doesn't say whether prompt, provider or model is at fault, so the check can't reliably judge prompt changes.

## Solution

Make the prompt state the exact reply shape (including the empty case), and make sure OpenRouter requests are strict-schema and only routed to providers that honour them. Then re-run golden several times. Only if still flaky, move to a stronger model via `Llm__Model` or pin a provider (env/config, not parser changes).

The parser stays strict: no tolerance for bare arrays or `{"text": ...}` steps. That would code around one model's quirk and hide drift on other models.

Already in place: the OpenRouter client sends `provider.require_parameters = true`, and the golden test prints extractor logs (raw bad reply, skipped entries). This spec adds test coverage for the request shape and verifies it in practice.

## User Stories

1. As the cook, I want a video with no Recipe to end as "no recipe found", so that I'm not shown a confusing generic failure
2. As the cook, I want all dishes of a multi-dish video imported, so that I don't re-add them by hand
3. As the cook, I want an Import to succeed reliably regardless of which OpenRouter provider serves it, so that retrying isn't a coin flip
4. As the developer, I want the prompt to state the exact reply shape, so that the model can't improvise the wrapper object
5. As the developer, I want the empty case spelled out as a literal empty `recipes` object, so that "empty list" isn't read as a bare array
6. As the developer, I want an example showing ingredient lines and steps as plain strings, so that the model doesn't wrap steps in objects
7. As the developer, I want requests sent with a strict JSON schema, so that providers that can enforce it do
8. As the developer, I want requests routed only to providers that honour `response_format`, so that schema enforcement isn't best-effort
9. As the developer, I want a test proving both request settings are sent, so that a refactor can't silently drop them
10. As the developer, I want a golden failure to show the raw reply or skipped titles, so that I can tell prompt drift from provider drift
11. As the developer, I want the parser to stay strict, so that schema drift on any model stays visible
12. As the developer, I want repeated golden runs to be stable, so that a pass means something
13. As the developer, I want escalation to a stronger model or a pinned provider to be config-only, so that I need no code change
14. As the developer, I want golden to stay opt-in and out of CI, so that CI cost stays low

## Implementation Decisions

- Extraction prompt: replace "leere Liste" with the literal empty reply `{"recipes": []}`. Keep the guidance on when no Recipe exists (chapter-list-only descriptions, `[Music]`-only transcripts).
- Extraction prompt: add a minimal example of the expected JSON — a `recipes` array whose entries carry title, servings, minutes, ingredient lines and steps, with ingredient lines and steps as plain strings. Use the existing field names; the schema is generated from the extraction records, so prompt and schema must agree. Keep the example obviously illustrative so the model doesn't copy its content.
- OpenRouter request: confirm the JSON-schema response format is sent with `strict: true`; if the chat library doesn't set it, set it. Keep `provider.require_parameters = true`.
- Extractor parsing and validation: unchanged.
- Model: default changed to `google/gemini-2.5-flash-lite` (done separately, before this spec is implemented). Escalate further only if golden is still flaky after the above; record the decision in the ticket.
- OpenRouter's Response Healing plugin is not used: it repairs malformed JSON, not schema-shaped drift, and is non-streaming only.
- Golden test and logging: keep the test-output logger.

## Testing Decisions

- A good test checks external behaviour: the request that goes out, or what the extractor returns for a given model reply. It does not assert on prompt wording.
- Prompt quality is judged only by the golden check (real model, real transcripts). Prompt text is not unit-tested.
- Request shape: one unit test at the LLM client factory seam, pointing the client at a local stub endpoint and asserting the outgoing body contains the strict JSON-schema response format and `provider.require_parameters = true`. Prior art: the existing client factory unit tests.
- Extractor strictness: existing extractor tests with the fake chat client cover bad replies → `LlmBadOutput`. Add a case only if a bare-array reply isn't covered.
- Verification: run golden at least 3 times and note per-video pass/fail (expected recipe counts 0, 1, 5) in the ticket comments.
- One new seam (the client factory); the prompt rides the existing golden seam.

## Out of Scope

- Making the parser accept bare arrays, object-shaped steps or other model quirks
- Changing the provider
- New golden videos or changed expected counts
- Running golden in CI
- Response Healing plugin
- Rotating the OpenRouter key printed in an earlier transcript (user action)

## Further Notes

- Prior context: tickets 11 (OpenRouter-only) and 12 (golden check on OpenRouter) in `video-import-build`.
- OpenRouter docs: structured outputs support varies by provider endpoint, and strict enforcement varies by provider.
- Golden runs cost real OpenRouter credits; keep repeats to a handful.
