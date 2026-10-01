# Video Import — wayfinder map

Label: wayfinder:map

## Destination

A resolved spec (`spec.md`) for Video Import (YouTube link → one or more German Recipes in the Library), incl. background Imports with Library progress UI and an LLM provider seam with Ollama (local Gemma) + OpenRouter adapters — ready to generate implementation tickets from.

## Notes

- Domain: root `CONTEXT.md` (Import, Web Import, Video Import, Recipe, Source, Library…). Use its terms.
- Builds on V1: [`recipejoe-v1/spec.md`](../recipejoe-v1/spec.md) (architecture, test layers, CI). Flag contradictions with it.
- Skills: grilling + domain-modeling for grilling tickets; `codebase-design` for seams (provider strategy, background Import); `research` for research tickets (write to `research/`, no branch — repo guardrail); `prototype` for UI.
- User is learning DevOps/testing: explain mechanics, keep CI cost-aware (no paid LLM calls in CI).

### Standing decisions (settled while charting)

- Video Import is a kind of Import alongside Web Import: different paths, same end (Recipes in Library).
- Input: youtube.com/watch, youtu.be, Shorts. No playlists/channels. Existing URL input auto-detects YouTube. Several pending Imports may run at once.
- Input to LLM: all available video text (transcript, title, description, chapters if any). Thumbnail becomes the Recipe image. No audio transcription (Whisper); no captions → failure.
- One video → N Recipes, each separate; all share the video as Source.
- Output always German, written as a proper recipe (LLM instructed on style), not a literal translation.
- Both Import kinds become background Imports (one mechanism). Library shows a pending card with stage label + indeterminate animation; failure turns it into an error card with "Erneut versuchen" / "Verwerfen". Newly imported Recipes are visibly marked in the Library.
- Save directly to Library, no review step.
- LLM providers: strategy seam, Ollama + OpenRouter adapters, chosen by config. Deployment itself out.
- Transcript code stays in .NET unless research shows it clearly falls short; a Python sidecar is acceptable then.
- Tests: fake provider in unit/API/E2E; optional local golden check against real Ollama, never in CI.
- Duplicates allowed (same as Web Import).

## Decisions so far

- [YouTube transcript + metadata access](issues/01-youtube-transcript-access.md): .NET-only via YoutubeExplode behind own seam; no sidecar; works from home IP, cloud IPs/CI likely blocked; tests use recorded JSON of our own shape.
- [LLM providers: Ollama + OpenRouter](issues/02-llm-providers-ollama-openrouter.md): local `gemma4:26b` via host.docker.internal, structured JSON works; `IChatClient` (Microsoft.Extensions.AI) under a domain extractor, OllamaSharp + OpenAI-SDK→OpenRouter factories; ~$0.0015/Import on OpenRouter.
- [Background Import mechanism](issues/03-background-import-mechanism.md): in-memory Imports (Pending/Failed, vanish on success), BackgroundService + Channel, `/api/imports` + polling, `IImportPath` seam, Recipes saved individually (≥1 saved = success)
- [LLM extraction seam](issues/04-llm-extraction-seam.md): `RecipeExtractor` (VideoText → `ParsedRecipe`s, owns prompt/schema/validation) over `IChatClient`; Video path attaches thumbnail + Source; prompt as embedded `.md`; `Llm:*` config; WireMock OpenRouter stub in API + E2E tests
- [Library Import progress UI](issues/05-library-import-progress-ui.md): variant A: Pending/Failed Imports as rows at top of list (Skeleton thumb, URL label, spinner + German stage label; inline retry/dismiss), N Recipes appear together with "Neu" badge
- [Video Import failure kinds](issues/06-video-import-failures.md): + `NoCaptions`, `LlmUnavailable`, `LlmBadOutput`; shared kinds reused, `ImportDto.kind` (Web|Video) picks Seite/Video wording; retry only for transient kinds (frontend map)
- ["Neu" marker semantics](issues/07-new-marker-semantics.md): clears on first Cook View open; `SeenAt` column (backfilled), `IsNew` on summary, GET detail sets it, Library query invalidated; both Import kinds
- [Long videos vs context window](issues/09-long-videos-context-window.md): videos are small (36-min DE = 9.2k tokens); fixed `num_ctx` 32768 via `Llm:ContextTokens` + `truncate:false` (Ollama otherwise silently drops the prompt front) → new non-retryable `VideoTooLong`; OpenRouter 262K, compression off; no chunking

## Not yet specified

<!-- for /to-spec: settle these while writing the spec; prompt + golden corpus becomes an implementation ticket -->

- Video source small calls: parse chapters vs raw description to LLM; WebP vs .jpg thumbnail (V1 `ImageDownloader` already accepts WebP); auto-translated-only track = captions?; track choice (research suggests manual over auto, original language).
- LLM output validation: what counts as a usable Recipe; partial results when 1 of N is malformed; Servings/timings often missing in videos (null vs estimate).
- Config + secrets: where the OpenRouter key lives locally and in compose; default provider for `just dev`; `Llm:Timeout` values + retry caps (depends on [Long videos vs context window](issues/09-long-videos-context-window.md)).
- Test plan for Video Import across layers (fixtures for transcripts, stub reply variants). Leaning: `VideoSource:Provider=Fake` reading recorded JSON for API/E2E; LLM via WireMock stub (see [LLM extraction seam](issues/04-llm-extraction-seam.md)).
- Prompt design + German style guide; golden transcript corpus (which videos, how judged): implementation ticket, needs real Ollama runs.
- Drag-and-drop links onto the Library.

## Out of scope

- Web Import translation (offer to translate non-German pages): later effort, reuses the LLM seam.
- Deployment to a web server (hosting, auth, prod secrets).
- Playlists/channels, audio transcription, review-before-save, duplicate detection.
- Cancelling a Pending Import; Imports surviving a backend restart (see [Background Import mechanism](issues/03-background-import-mechanism.md)).
