# Video Import — spec

**Status:** ready-for-agent

**Origin:** wayfinder map [`map.md`](map.md) (decisions in `issues/01`–`07`). Builds on [`recipejoe-v1/spec.md`](../recipejoe-v1/spec.md). Terms follow [`CONTEXT.md`](../../CONTEXT.md) (Import, Web Import, Video Import, Recipe, Recipe Draft, Source, Library, New Recipe, Cook View); architecture terms follow `/codebase-design`.

## Problem Statement

Many recipes the cook wants to keep only exist as YouTube videos. These are often in English, often several recipes in one video, and there's no structured data to Import. Today the only way to get one into the Library is to watch the video and type the Recipe in by hand.

Importing is also blocking. The Import dialog waits until the page is fetched and parsed. A Video Import that takes a minute or more (a local LLM writing the Recipe) would freeze the dialog, and the cook couldn't start a second Import meanwhile.

## Solution

The cook pastes a YouTube link (watch, youtu.be or Shorts) into the existing Import dialog. RecipeJoe detects it's a video, reads the video's text (title, description, transcript) and its thumbnail, and asks an LLM to write one German Recipe per dish shown in the video. The Recipes are saved straight to the Library, each with the video as Source and the thumbnail as image.

Every Import, Web and Video, now runs in the background. The dialog closes right away. The Library shows each Pending Import as a row at the top, with a German stage label and a spinner. A failed Import turns into an error row with a reason and "Erneut versuchen" / "Verwerfen". When an Import succeeds, its row disappears and its Recipes appear at the top, marked "Neu" until they're first opened in Cook View. Several Imports can run at once.

The LLM is behind a provider seam with two adapters: Ollama (local Gemma, the default for development) and OpenRouter (cheap hosted Gemma). Config picks one.

## User Stories

1. As a cook, I want to paste a youtube.com/watch link into the Import dialog, so that I can Import a recipe from a video.
2. As a cook, I want youtu.be short links to work, so that links shared from the YouTube app Import too.
3. As a cook, I want YouTube Shorts links to work, so that short recipe clips can be Imported.
4. As a cook, I want the same input field for web pages and videos, so that I don't have to say which kind of link it is.
5. As a cook, I want the input placeholder to read "Webadresse oder YouTube-Link", so that I know videos are accepted.
6. As a cook, I want an invalid URL to be rejected inline in the dialog, so that I can fix a typo right away.
7. As a cook, I want the dialog to close as soon as I submit a valid URL, so that I can keep browsing or start another Import.
8. As a cook, I want each running Import shown as a row at the top of the Library, so that I can see it's being worked on.
9. As a cook, I want a Pending row to show the URL label (host + path, no `www.`), so that I can tell my Imports apart.
10. As a cook, I want a Pending row to show a video or web icon, so that I can see which kind of Import it is.
11. As a cook, I want a Pending Video Import to show "Video wird geladen…", then "Rezept wird geschrieben…", then "Wird gespeichert…", so that I know roughly what's happening.
12. As a cook, I want a Pending Web Import to show "Seite wird geladen…", then "Rezept wird gelesen…", then "Wird gespeichert…", so that both kinds feel consistent.
13. As a cook, I want a spinner instead of a progress bar or percentage, so that I'm not given a false sense of precision.
14. As a cook, I want several Imports to run at once, so that I can queue up a batch of links.
15. As a cook, I want Pending and Failed rows to stay visible while I search the Library, so that I don't lose track of them.
16. As a cook, I want a finished Import's row to disappear and its Recipes to appear at the top of the Library, so that I see the result without reloading.
17. As a cook, I want all Recipes from one video to appear together, so that a multi-recipe video lands as one batch.
18. As a cook, I want a video showing several dishes to become several separate Recipes, so that each dish can be found and cooked on its own.
19. As a cook, I want every Recipe from a video to have the video as its Source, so that I can go back to watch it.
20. As a cook, I want the video thumbnail as each Recipe's image, so that the Library looks like the video I remember.
21. As a cook, I want Video Import Recipes written as proper German recipes, even from English videos, so that my Library reads in one language.
22. As a cook, I want the Recipe written in recipe style (clear Ingredient Lines, ordered Steps), not as a literal transcript translation, so that I can cook from it.
23. As a cook, I want Servings and timings left empty when the video doesn't state them, so that the Recipe never shows made-up numbers.
24. As a cook, I want Imported Recipes marked "Neu" in the Library, so that I can find what just arrived.
25. As a cook, I want the "Neu" mark to disappear after I first open a Recipe in Cook View, so that the mark means "not looked at yet".
26. As a cook, I want Web Import Recipes marked "Neu" too, so that both Import kinds behave the same.
27. As a cook, I want my existing Recipes not marked "Neu" after the upgrade, so that the mark stays meaningful.
28. As a cook, I want a failed Import to stay as an error row with a German reason, so that I know what went wrong.
29. As a cook, I want a video without captions to fail with "Dieses Video hat keine Untertitel, daraus kann kein Rezept gelesen werden.", so that I know retrying won't help.
30. As a cook, I want a private, removed or restricted video to fail with "Dieses Video ist nicht verfügbar (privat, gelöscht oder eingeschränkt).", so that I know the link is dead.
31. As a cook, I want a video with no recipe in it to fail with "In diesem Video wurde kein Rezept gefunden.", so that I know it wasn't a technical problem.
32. As a cook, I want YouTube being unreachable or blocking us to show its own message, so that I know to try later.
33. As a cook, I want an unreachable LLM provider to show "Der LLM-Provider ist nicht erreichbar.", so that I know to start Ollama or check the config.
34. As a cook, I want unusable LLM output to show "Die KI hat keine brauchbare Antwort geliefert. Versuch es nochmal.", so that I know a retry may help.
35. As a cook, I want "Erneut versuchen" only on failures where retrying can help, so that I don't retry in vain.
36. As a cook, I want retrying to turn the error row back into a Pending row in place, so that I can follow the second attempt.
37. As a cook, I want "Verwerfen" to remove a Failed Import, so that the Library stays tidy.
38. As a cook, I want a video where only some dishes could be written to still save those Recipes, so that one bad dish doesn't lose the rest.
39. As a cook, I want to Import the same video twice if I choose to, so that duplicates behave as they do for Web Import.
40. As a cook, I want Imports lost after a backend restart to simply vanish, so that the Library never shows a stuck row.
41. As a developer, I want one background Import mechanism for both Import kinds, so that lifecycle, retry and UI are written once.
42. As a developer, I want Import paths behind one internal seam (URL → Recipe Drafts or failure, with stage progress), so that adding an Import kind doesn't touch the runner.
43. As a developer, I want the existing Web Import parser reused unchanged as the Web path, so that its unit and corpus tests keep passing.
44. As a developer, I want YouTube access behind my own video source seam, so that YoutubeExplode can later be swapped for yt-dlp.
45. As a developer, I want the prompt, output schema and validation owned by one Recipe extractor, so that changing how Recipes are written touches one module.
46. As a developer, I want the prompt and German style guide in git as an embedded Markdown file, so that prompt changes are reviewed like code.
47. As a developer, I want the LLM output schema generated from a C# record, so that the schema and the parser can't drift.
48. As a developer, I want the LLM provider chosen by config (Ollama or OpenRouter), so that I can switch without code changes.
49. As a developer, I want invalid LLM config to fail at startup, so that I find mistakes before the first Import.
50. As a developer, I want `just dev` to default to local Ollama, so that development costs nothing.
51. As a developer, I want the OpenRouter key in a git-ignored `.env`, so that it never lands in git.
52. As a developer, I want the backend container to reach host Ollama, so that the compose stack works on macOS.
53. As a developer, I want no live YouTube or paid LLM calls in CI, so that CI is deterministic and free.
54. As a developer, I want API and E2E tests to use a fake video source (recorded JSON in my own shape), so that tests never hit YouTube.
55. As a developer, I want API and E2E tests to use a WireMock stub in place of OpenRouter, so that the real HTTP adapter is exercised without cost.
56. As a developer, I want an optional local golden check against real Ollama, so that I can judge prompt changes on real videos.
57. As a developer, I want LLM failure details only in logs, so that the API exposes just the failure kind.
58. As a developer, I want the Library to poll Imports only while one is Pending, so that an idle app makes no requests.

## Implementation Decisions

### Background Import (both kinds)

- **Imports module** (deep, backend): Start / List / Retry / Dismiss + runner + store. In memory only: a singleton keyed by Import id. Restarting loses Pending and Failed Imports (accepted). The frontend treats a vanished Import as finished.
- **States:** Pending (with stage) | Failed (with kind). No Completed state, no history. Stages: `Fetching` → `Extracting` → `Saving` (UI wording per Import kind, see stories 11–12).
- **Runner:** a hosted background service reads Import ids from an in-process channel. Concurrency `Import:MaxConcurrent` (default 2). Each run gets its own DI scope.
- **API:**
  - `POST /api/imports {url}` → 202 + ImportDto. `InvalidUrl` is the only synchronous failure (400, no Import created). Everything else, incl. `ForbiddenAddress`, ends up on the row.
  - `GET /api/imports` → all Pending and Failed Imports.
  - `POST /api/imports/{id}/retry` → 202. Re-runs from scratch with the same id, back to Pending.
  - `DELETE /api/imports/{id}` → 204. Dismisses a Failed Import.
  - Cancelling a Pending Import is out of scope.
- **ImportDto:** id, url, kind (`Web`|`Video`), state (`Pending`|`Failed`), stage (when Pending), failure kind (when Failed).
- **Internal seam `IImportPath`:** `RunAsync(Uri, IProgress<ImportStage>, ct) → Result<IReadOnlyList<RecipeDraft>, ImportFailure>`. The path is chosen by URL (YouTube host → Video, else Web) and sets the Import kind.
- **Web path** wraps the existing `Importer` unchanged (one Draft).
- **Saving:** each Recipe Draft in its own transaction. ≥1 saved → success, Import removed. 0 saved → Failed. Recipes don't reference their Import; they carry their Source.
- **Web Import migration:** remove `POST /api/recipes/import`. Regenerate the OpenAPI client.

### Video source

- Own seam: URL → `VideoText(Title, Description, Transcript)` + thumbnail URL | typed failure. One production adapter, YoutubeExplode (.NET only, no Python sidecar). A `Fake` adapter reads recorded JSON in our own shape, selected by `VideoSource:Provider=Fake` (API/E2E).
- URL forms: youtube.com/watch, youtu.be, /shorts/. No playlists or channels.
- Track choice: manual over auto-generated, original language first. YouTube auto-translation isn't used.
- Chapters: no parser. The raw description goes to the LLM, which uses `0:00 …` lines itself. The `Chapters` field from issue 04 is dropped.
- Thumbnail: best available. WebP is fine (`ImageDownloader` already accepts it). Downloaded once and shared by all N Drafts. A failed thumbnail leaves the Recipes without an image (same as Web Import).
- Failure mapping: network error → `Unreachable`; private/removed/restricted → `NotFound`; bot block, incl. a listed caption track that comes back empty → `Blocked`; no caption tracks → `NoCaptions`. No audio transcription.

### Recipe extractor (LLM)

- Domain module `RecipeExtractor.ExtractAsync(VideoText, ct) → Result<IReadOnlyList<ParsedRecipe>, ImportFailure>`. It owns the prompt, schema, validation and timeout. It's text in, text out: `ImageUrl` is always null, and the LLM never sees or emits URLs.
- Provider seam below it: `IChatClient` (Microsoft.Extensions.AI). Two thin factories, picked by `Llm:Provider`: OllamaSharp (native API, per-request `num_ctx`, `think:false`, `keep_alive`) and OpenAI SDK → OpenRouter base URL (`provider.require_parameters: true`). No `Fake` provider.
- LLM output schema, generated from a C# record via `GetResponseAsync<T>`:
  `{recipes:[{title, servings?, prepMinutes?, cookMinutes?, totalMinutes?, ingredientLines[], steps[]}]}`. Minutes are integers mapped to `TimeSpan?`; servings is free text.
- Prompt + German style guide: an embedded Markdown resource beside the extractor. It tells the model to write German recipe style, leave Servings/timings null unless stated, and output one entry per dish.
- Validation: a usable Recipe has a non-empty title, ≥1 Ingredient Line and ≥1 Step. Invalid entries are skipped and logged.
- Failure mapping:
  - empty `recipes` → `NoRecipe`
  - all entries invalid, malformed JSON or schema violation → `LlmBadOutput`
  - network, timeout, 5xx, 429, 401 or unknown model → `LlmUnavailable` (details only in logs)
- **Video path** (`VideoImportPath : IImportPath`): video source → extractor → for each ParsedRecipe a RecipeDraft with Source = video URL and the shared thumbnail image. Reports the stages.

### Failure kinds

- `ImportFailure` gains `NoCaptions`, `LlmUnavailable`, `LlmBadOutput`. Shared kinds are reused where the cause matches.
- The frontend picks the wording by (Import kind, failure kind). Web wording stays as is; Video wording is in the table in issue 06.
- Retryability is a frontend map, not part of the API:
  - Retry: Unreachable, Blocked, BadResponse, LlmUnavailable, LlmBadOutput.
  - Dismiss only: NotFound, NoCaptions, ForbiddenAddress, NoRecipe.

### New Recipe marker

- Nullable `SeenAt` on Recipe. The migration backfills `SeenAt = CreatedAt`. Seeded Recipes show "Neu" (no special handling).
- `RecipeSummaryDto.IsNew` (= `SeenAt == null`).
- `GET /api/recipes/{id}` sets `SeenAt` when it's null (an accepted GET side effect).
- Frontend: when the Cook View query succeeds, invalidate the Library query.

### Frontend

- The Recipe data module (from the recipe-data-module spec) gains the Import reads and writes: list Imports, start, retry, dismiss.
  - Polling: `refetchInterval` ~1.5 s while any Import is Pending, off otherwise.
  - When an Import vanishes from the list, invalidate the Library.
  - Features never touch cache keys.
- Library: variant A from the prototype. Pending/Failed rows sit at the top in the Recipe row shape:
  - Pending: Skeleton thumbnail with a kind icon, URL label, spinner + stage label.
  - Failed: destructive icon, message, inline "Erneut versuchen" (outline) / "Verwerfen" (ghost).
  - shadcn `Badge` "Neu" next to the title. Add `Skeleton` via the shadcn CLI.
- Import dialog: closes on 202; only `InvalidUrl` stays inline; placeholder "Webadresse oder YouTube-Link". Cook View no longer opens automatically after an Import.

### Config + infra

- `Llm:Provider` (`Ollama`|`OpenRouter`), `Llm:Model`, `Llm:BaseUrl`, `Llm:ApiKey`, `Llm:Timeout`, validated on start.
  - Defaults: Ollama, `gemma4:26b`, `http://host.docker.internal:11434`, timeout 3 min. SDK retries off (the cook retries).
  - OpenRouter model: `google/gemma-4-26b-a4b-it`.
- OpenRouter key: in the git-ignored `.env` (documented in `.env.example`), passed to the backend through compose. Never in CI.
- Compose backend gets `extra_hosts: host.docker.internal:host-gateway`. Ollama stays bound to 127.0.0.1 on the host.
- `compose.e2e.yaml`:
  - adds a WireMock container as the OpenRouter stub (`Llm:Provider=OpenRouter`, `Llm:BaseUrl=http://llm-stub`); its response template echoes the video title from the request.
  - sets `VideoSource:Provider=Fake`.
- **Long videos** (issue 09): `Llm:ContextTokens` (default 32768) sent as constant `num_ctx`; `"truncate": false` injected so Ollama overflow is a 400, not silent front-truncation; overflow (Ollama or OpenRouter) → new non-retryable kind `VideoTooLong`. No chunking. OpenRouter `context-compression` stays off.

## Testing Decisions

- A good test drives a module through its public interface and asserts on behaviour the cook or caller can see (API responses, saved Recipes, rendered rows). It never asserts on internal calls, prompt text or cache keys.
- **Imports API (main seam):** integration tests (real Postgres, `ApiFactory`) start Imports via `POST /api/imports` and poll `GET /api/imports` until the Import is gone or Failed, then check the Library.
  - Video path: fake video source + in-process WireMock.Net as the OpenRouter stub.
  - Covers: N Recipes saved with Source + image; each failure kind reachable (NoCaptions, NotFound, Blocked, LlmUnavailable via stub 5xx/timeout, LlmBadOutput via malformed reply, NoRecipe via empty list); retry; dismiss; InvalidUrl 400.
  - Web Import API tests move from the removed endpoint to these endpoints.
  - Prior art: `ImportRecipeTests`, `HttpFetcherTests` (WireMock).
- **Imports module lifecycle:** unit tests with fake `IImportPath`s for stage progression, concurrency limit, retry from scratch, partial save (≥1 saved = success, 0 = Failed) and dismiss. Prior art: `ImporterTests`.
- **RecipeExtractor:** unit tests against a fake `IChatClient` with recorded replies: valid multi-recipe output, minutes → TimeSpan, missing Servings/timings stay null, partially invalid output (skipped), all invalid → LlmBadOutput, malformed JSON, empty list → NoRecipe, transport errors → LlmUnavailable. Prior art: `RecipeParserTests`.
- **Video source YoutubeExplode adapter:** no live tests in CI. Track choice and failure mapping are tested where they're pure logic.
- **Web path:** `Importer` unit + corpus tests untouched.
- **Frontend:** route-level tests with MSW through the shared provider tree.
  - Covers: Pending rows with stage labels, Failed rows with per-(kind, failure) message, retry button only on retryable kinds, retry/dismiss requests, rows visible while searching, polling stops when idle, Recipes appear when the Import vanishes, "Neu" badge and its clearing after Cook View, dialog closes on 202 and shows InvalidUrl inline.
  - Prior art: `LibraryPage.test.tsx`, `ImportDialog.test.tsx`, `failureMessage.test.ts`, `deleteFlow.test.tsx`.
- **E2E (Playwright, compose.e2e):**
  - Web Import: dialog → row → Recipe in Library (rewrites the current fixme test).
  - Video Import: fake video → WireMock reply echoing the title → Recipe(s) in Library with "Neu"; open, go back, badge gone.
  - Prior art: `import.spec.ts`.
- **Golden check (optional, local only, never CI):** runs the extractor against real Ollama on the candidate videos (i84Sc5uvQa8, 6tMZNYQkycI with 10 recipes, 6wR2T-PexT4 long DE). It's a separate implementation ticket together with prompt design.
- **Contract test:** the pending-model-changes test covers the `SeenAt` migration.

## Out of Scope

- Web Import translation (a later effort that will reuse the LLM seam).
- Deployment (hosting, auth, production secrets, proxies for YouTube from cloud IPs).
- Playlists, channels, audio transcription (Whisper), videos without captions.
- A review step before saving; duplicate detection.
- Cancelling a Pending Import; Imports surviving a backend restart; Import history.
- SSE/WebSocket updates (polling only).
- Drag-and-drop links onto the Library.
- Paid LLM or live YouTube calls in CI.

## Further Notes

- Issue 09 resolved: see Long videos above. A video whose transcript holds no recipe text (e.g. 6tMZNYQkycI, only `[Music]`, recipes on screen) fails with `NoRecipe`.
- Prompt design + German style guide + golden corpus need real Ollama runs. Treat them as their own ticket, after the extractor seam exists.
- YouTube risks: PO tokens or bot blocks can make caption fetches come back empty (mapped to Blocked). Expect a YoutubeExplode NuGet bump every few months (Renovate).
- Runtime: about 60–80 s per 30-min video on local Gemma. `OLLAMA_NUM_PARALLEL=1` means concurrent Video Imports queue at Ollama. OpenRouter costs about $0.0015 per Import.
- Spec deviation from V1: Import no longer opens Cook View automatically. Recipes land in the Library instead.
