# 04: Video Import via API (tracer bullet)

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decisions: [LLM extraction seam](../../video-import/issues/04-llm-extraction-seam.md), [LLM providers](../../video-import/issues/02-llm-providers-ollama-openrouter.md), [YouTube transcript access](../../video-import/issues/01-youtube-transcript-access.md)

**What to build:** Starting an Import with a YouTube URL (watch, youtu.be, Shorts) runs the Video path. It reads the video's text and thumbnail from the video source, the LLM writes one Recipe per dish, and all of them are saved to the Library with the video as Source and the thumbnail as image. The path is proven end to end against a Fake video source and an OpenRouter stub. The real YouTube adapter is ticket 07 and the Ollama adapter is ticket 08.

**Blocked by:** 01

**Status:** resolved

- [ ] Video source seam: URL → video text (title, description, transcript) + thumbnail URL | typed failure; `Fake` adapter reads recorded JSON in our own shape, selected by `VideoSource:Provider=Fake`
- [ ] YouTube URLs (watch, youtu.be, /shorts/) pick the Video path and Import kind `Video`; all others pick Web
- [ ] `RecipeExtractor` (video text → ParsedRecipes | failure) owns the prompt (embedded Markdown resource, first draft), the output schema generated from a C# record, validation (non-empty title, ≥1 Ingredient Line, ≥1 Step; invalid ones skipped and logged) and the timeout; integer minutes → durations; Servings/timings stay null when absent
- [ ] Provider seam `IChatClient`; OpenRouter factory (OpenAI SDK, OpenRouter base URL, `require_parameters`); SDK retries off
- [ ] `Llm:Provider|Model|BaseUrl|ApiKey|Timeout` bound and validated on start (timeout default 3 min)
- [ ] Video path reports stages; thumbnail downloaded once and shared by all N Drafts; a failed thumbnail leaves the Recipes without an image
- [ ] Extractor unit tests (fake `IChatClient`): multi-recipe reply, null Servings/timings, partially invalid reply skips entries
- [ ] API integration test (Fake video source + in-process WireMock.Net OpenRouter stub): one video → N Recipes in the Library, each with Source + image; ImportDto kind `Video`
- [ ] Coverage gate passes; no live YouTube or LLM calls in any test
