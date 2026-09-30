# 03: API tests for Blocked and BadResponse

**Spec:** [../spec.md](../spec.md)

**What to build:** Canned fixture responses are usable from API-level tests via the test API factory, so the HTTP status and problem body for an Import failure can be checked end to end.

**Blocked by:** 02 (Canned fixture responses + per-rule Importer tests)

**Status:** done

- [x] Test API factory lets a test register canned responses on the fixture adapter
- [x] API test: Blocked Import returns the expected status code and problem `kind`
- [x] API test: BadResponse Import returns the expected status code and problem `kind`
