# 02: Canned fixture responses + per-rule Importer tests

**Spec:** [../spec.md](../spec.md)

**What to build:** A test can register a canned raw response (status, headers, body) for a URL on the fixture adapter, so every failure kind is reachable through the Importer. The fixture adapter becomes the only test adapter.

**Blocked by:** 01 (Fetch policy owns response classification)

**Status:** resolved

- [ ] Fixture adapter serves registered per-URL canned responses; unregistered unknown URLs still 404
- [ ] Importer tests, one per rule: Blocked via 403, 429, challenge header, 503 challenge page, 200 challenge page; NotFound via 410; BadResponse via 500, non-HTML page, over-cap body
- [ ] Tests assert failure kinds / resulting Recipe Drafts, not classification internals
- [ ] Ad-hoc stub fetchers in Importer and image downloader tests replaced by the fixture adapter
- [ ] Image downloader over-cap tests for the removed check dropped
