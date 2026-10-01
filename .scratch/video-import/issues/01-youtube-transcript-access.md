# YouTube transcript + metadata access

Type: research
Status: resolved
Blocked by: —

## Question

How can RecipeJoe get a YouTube video's transcript (manual + auto-generated captions, any language), title, description, chapters and thumbnail from a watch/youtu.be/Shorts URL? Compare Python options (e.g. youtube-transcript-api, yt-dlp) with .NET options (e.g. YoutubeExplode) on: reliability, maintenance activity, mechanism (unofficial endpoints), known breakage/bot-blocking, rate limits, licence, behaviour without captions, and running from a Docker container. Also: the official YouTube Data API v3 — what it can and can't give (captions need OAuth?). Recommend .NET-only vs a Python sidecar (standing decision: stay .NET unless it clearly falls short). Note what tests could record as fixtures.

Research: [`research/youtube-transcript-access.md`](../../../research/youtube-transcript-access.md) (no branch: repo guardrail blocks agent git writes)

## Answer

- **.NET-only, YoutubeExplode 6.6.2** (MIT + README terms-of-use; managed, net10.0, injectable `HttpClient`). Verified locally: watch/youtu.be/Shorts URLs, title, description, thumbnail, manual + auto caption tracks in any language. No captions → empty track list (no exception).
- Python sidecar rejected: all unofficial libs hit the same YouTube endpoints and the same blocking. youtube-transcript-api = transcripts only; yt-dlp = most complete but needs Python + Deno.
- Chapters: YoutubeExplode has none; creator chapters are `0:00 …` lines in the description (own parser or let LLM read description). Auto-generated chapters lost.
- YouTube Data API v3: can't fetch others' captions (OAuth + edit permission). Not used.
- Risks: PO tokens can make caption fetches return empty; cloud/datacenter IPs blocked → fine locally, deployment would need proxies; no live YouTube calls in CI. Expect NuGet bumps every few months.
- Design: own seam (URL → title, description, chapters, transcript, thumbnail URL | typed failure: not YouTube / unavailable / no captions / blocked); YoutubeExplode is the one adapter, yt-dlp swappable later. Tests fake the seam with recorded JSON in our own shape (not YouTube HTTP). Candidate videos: i84Sc5uvQa8, 6tMZNYQkycI (10 recipes), 6wR2T-PexT4 (long DE, noisy), MGKYhCRwrN0 (no captions), dQw4w9WgXcQ (manual + auto).
- Open small calls (for later tickets): parse chapters vs pass raw description; WebP vs forced .jpg thumbnail; does an auto-translated-only track count as captions.
