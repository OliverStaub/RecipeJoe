# 12: HttpClient fetcher + SSRF guard

**Spec:** [../../recipejoe-v1/spec.md](../../recipejoe-v1/spec.md) §2.5 (Fetching, Failure kinds); decision: [Import fetching](../../recipejoe-v1/issues/11-import-fetching.md)

**What to build:** Importing a real Recipe URL works in dev. Unreachable, blocked, missing and unreadable pages each fail with their own kind, and private addresses can't be reached.

**Blocked by:** 09 (Import a plain Recipe via the API)

**Status:** resolved

- [x] HttpClient `IPageFetcher` adapter registered in prod: Chrome UA, `Accept`, `Accept-Language: de-DE,de;q=0.9,en;q=0.8`, gzip/br
- [x] 10 s total timeout, 5 MB streamed cap; manual redirects (max 5, http/https only); no retries
- [x] The page must be `text/html` or `application/xhtml+xml`; charset from the header, else from the meta tag
- [x] Failure mapping:
  - 401/402/403/429 and challenge pages → `Blocked`
  - 404/410 → `NotFound`
  - other non-2xx, not HTML, too large, too many redirects → `BadResponse`
  - DNS, connection or timeout failure → `Unreachable`
- [x] SSRF guard in `ConnectCallback` on the connected IP; pure `IsPublic(IPAddress)` per §2.5 → `ForbiddenAddress`
- [x] `Import:AllowedHosts` bypass, with `localhost` in the Development settings
- [x] Unit: `IsPublic` table tests
- [x] Integration: WireMock.Net on loopback (status mapping, redirects, caps, timeouts); loopback without the allowlist → `ForbiddenAddress`
