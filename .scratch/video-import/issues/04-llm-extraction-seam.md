# LLM extraction seam

Type: grilling
Status: resolved
Blocked by: 02

## Question

What is the provider strategy's interface and who owns what? Decide: seam shape (video text bundle → N Recipe Drafts | failure) vs a lower-level chat seam with the prompt kept in the domain; the JSON output schema the LLM fills (mapped to Recipe fields); where the prompt + German style instructions live; provider selection via config; how it plugs into the Video Import path; the fake provider for tests.

## Answer

- **Seam (two layers)**: domain module `RecipeExtractor.ExtractAsync(VideoText, ct) → Result<IReadOnlyList<ParsedRecipe>, ImportFailure>` — owns prompt, schema, validation, timeout. Below it the provider seam `IChatClient` (Microsoft.Extensions.AI), two adapters: OllamaSharp, OpenAI SDK → OpenRouter base URL.
- **Input**: `VideoText(Title, Description, Transcript, Chapters?)`; extractor alone decides prompt layout. Chapter parsing stays fog.
- **Output**: content only, reusing `ParsedRecipe`; extractor leaves `ImageUrl` null (LLM never sees/emits URLs). Empty `recipes` → `NoRecipe`. Other kinds + retryability → [Video Import failure kinds](06-video-import-failures.md).
- **LLM JSON schema**: `{recipes:[{title, servings?, prepMinutes?, cookMinutes?, totalMinutes?, ingredientLines[], steps[]}]}` — integer minutes (not ISO 8601) → `TimeSpan?`; servings free text. Generated from a C# record via `GetResponseAsync<T>` (no drift).
- **Prompt + German style guide**: embedded `.md` resource beside the extractor; in git, not config.
- **Draft mapping**: `VideoImportPath : IImportPath` composes video source → extractor → sets `ImageUrl` = video thumbnail on each, downloads it once via `ImageDownloader`, shares the `ImageFile` across all N Drafts, Source = video URL. Mirrors `Importer` (ParsedRecipe → RecipeDraft). Extractor stays text-in/text-out.
- **Config**: `Llm:Provider` (`Ollama`|`OpenRouter`), `Llm:Model`, `Llm:BaseUrl`, `Llm:ApiKey`, `Llm:Timeout`; validated on start. No `Fake` provider.
- **Test doubles**: extractor unit tests fake `IChatClient` (recorded replies, malformed/partial cases). API integration tests: WireMock.Net in-process as OpenRouter stub. E2E: WireMock container in `compose.e2e.yaml` (`Llm:Provider=OpenRouter`, `Llm:BaseUrl=http://llm-stub`), response template echoes the video title from the request → E2E token stays unique. Real Ollama only in the optional local golden check.
- **Adjacent (for Test plan fog)**: video source in API/E2E via `VideoSource:Provider=Fake` reading recorded JSON in our own shape; no YouTube HTTP stub.
