# 07: YoutubeExplode adapter

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [YouTube transcript access](../../video-import/issues/01-youtube-transcript-access.md) · research: [youtube-transcript-access.md](../../../research/youtube-transcript-access.md)

**What to build:** Video Import reads real YouTube videos. For a watch, youtu.be or Shorts link, the production video source returns the title, the full description, the best caption track as transcript, and the best thumbnail. If that isn't possible, it fails with the typed reason the cook will see.

**Blocked by:** 04

**Status:** resolved

- [x] YoutubeExplode adapter behind the video source seam; production default (`VideoSource:Provider`), injectable `HttpClient`
- [x] Track choice: manual over auto-generated, original language first; no auto-translation
- [x] Raw description passed through as is (no chapter parsing)
- [x] Best available thumbnail URL (WebP fine)
- [x] Failure mapping: network error → `Unreachable`; private/removed/restricted → `NotFound`; bot block, incl. a listed track returning empty → `Blocked`; no tracks → `NoCaptions`
- [x] Unit tests for the pure logic (track choice, mapping) without live calls; no live YouTube in CI
- [x] Manually verified locally on the candidate videos (i84Sc5uvQa8, 6wR2T-PexT4, MGKYhCRwrN0 no captions, dQw4w9WgXcQ manual + auto); results noted in Comments
- [x] Coverage gate passes

## Comments

Manual run 2026-10-01 (home IP, YoutubeExplode 6.6.2, via `YoutubeExplodeVideoSource`):

- `i84Sc5uvQa8` (watch): OK, title, 1995-char description, 6743-char transcript, maxres .jpg thumbnail
- `6wR2T-PexT4` (youtu.be): OK, 29398-char transcript, maxres .webp thumbnail
- `MGKYhCRwrN0` (Shorts): `NoCaptions`
- `dQw4w9WgXcQ` (manual + auto): OK, manual track chosen, 2089-char transcript
- Unknown id `aaaaaaaaaaa`: `NotFound`
- Not exercised live: `Blocked` (empty track), `Unreachable`; covered by unit tests of the mapping only.
