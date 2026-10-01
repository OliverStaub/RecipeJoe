# 07: YoutubeExplode adapter

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [YouTube transcript access](../../video-import/issues/01-youtube-transcript-access.md) · research: [youtube-transcript-access.md](../../../research/youtube-transcript-access.md)

**What to build:** Video Import reads real YouTube videos. For a watch, youtu.be or Shorts link, the production video source returns the title, the full description, the best caption track as transcript, and the best thumbnail. If that isn't possible, it fails with the typed reason the cook will see.

**Blocked by:** 04

**Status:** ready-for-agent

- [ ] YoutubeExplode adapter behind the video source seam; production default (`VideoSource:Provider`), injectable `HttpClient`
- [ ] Track choice: manual over auto-generated, original language first; no auto-translation
- [ ] Raw description passed through as is (no chapter parsing)
- [ ] Best available thumbnail URL (WebP fine)
- [ ] Failure mapping: network error → `Unreachable`; private/removed/restricted → `NotFound`; bot block, incl. a listed track returning empty → `Blocked`; no tracks → `NoCaptions`
- [ ] Unit tests for the pure logic (track choice, mapping) without live calls; no live YouTube in CI
- [ ] Manually verified locally on the candidate videos (i84Sc5uvQa8, 6wR2T-PexT4, MGKYhCRwrN0 no captions, dQw4w9WgXcQ manual + auto); results noted in Comments
- [ ] Coverage gate passes
