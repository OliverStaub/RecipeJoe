# 05: Video Import failures

**Spec:** [../../video-import/spec.md](../../video-import/spec.md) · decision: [Video Import failure kinds](../../video-import/issues/06-video-import-failures.md)

**What to build:** A failed Video Import shows the cook a German reason in video wording, and offers "Erneut versuchen" only where retrying can help. The new causes are a video without captions, an unreachable LLM and unusable LLM output. Shared causes (unreachable, blocked, not found, no recipe) reuse the existing kinds, and their wording is picked by Import kind.

**Blocked by:** 02, 04

**Status:** resolved

- [ ] `ImportFailure` gains `NoCaptions`, `LlmUnavailable`, `LlmBadOutput`
- [ ] Extractor mapping: network/timeout/5xx/429/401/unknown model → `LlmUnavailable` (details only in logs); malformed JSON, schema violation or all entries invalid → `LlmBadOutput`; empty list → `NoRecipe`
- [ ] Fake video source can produce `Unreachable`, `NotFound`, `Blocked`, `NoCaptions`
- [ ] Frontend message by (Import kind, failure kind): Video wording as in the decision table; Web wording unchanged
- [ ] Frontend retryability map: retry for Unreachable, Blocked, BadResponse, LlmUnavailable, LlmBadOutput; dismiss only for NotFound, NoCaptions, ForbiddenAddress, NoRecipe
- [ ] Extractor unit tests for each mapping
- [ ] API tests: each Video failure kind reachable as a Failed Import (stub 5xx/timeout, malformed reply, empty list; fake source failures)
- [ ] Unit/route tests: message per (kind, failure); retry button present only for retryable kinds
- [ ] Coverage gate passes
