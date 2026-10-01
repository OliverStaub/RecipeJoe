# 06: Video Import in the UI + E2E

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decisions: [Library Import progress UI](../../video-import/issues/05-library-import-progress-ui.md), [LLM extraction seam](../../video-import/issues/04-llm-extraction-seam.md)

**What to build:** The cook pastes a YouTube link into the same dialog. The Library shows a video row with Video stage labels, and when the Import ends, all the video's Recipes appear together, marked "Neu" once 03 has landed. The full path runs in E2E against a WireMock LLM stub and the Fake video source, with no YouTube or paid calls.

**Blocked by:** 02, 04

**Status:** resolved

- [x] Dialog placeholder "Webadresse oder YouTube-Link"
- [x] Pending row for a Video Import: video icon; "Video wird geladen…" / "Rezept wird geschrieben…" / "Wird gespeichert…"
- [x] `compose.e2e.yaml`: a WireMock container as the OpenRouter stub (`Llm:Provider=OpenRouter`, `Llm:BaseUrl` points at it), its response template echoes the video title from the request; backend uses `VideoSource:Provider=Fake` with E2E fixture videos
- [x] Route-level tests: video icon + Video stage labels; N Recipes appear when the Import vanishes
- [x] E2E: paste YouTube link → video row → Recipe(s) with the unique title in the Library with the video as Source
- [x] CI runs the new E2E without secrets or external calls
- [x] Coverage gate passes
