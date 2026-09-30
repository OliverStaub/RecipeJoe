# 01: Fetch policy owns response classification (one-method seam)

**Spec:** [../spec.md](../spec.md)

**What to build:** Every Import fetch goes through one fetch policy inside the Import module, which classifies a raw response into fetched content or an `ImportFailure`, the same way for the HTTP and fixture adapters. The fetch seam shrinks to one method (URL + fetch kind) returning a raw response (status, Content-Type, challenge-mitigation header, body or "over the cap", final URL) or a transport failure (Unreachable, ForbiddenAddress, BadResponse). The cook sees no change.

**Blocked by:** None (can start immediately)

**Status:** resolved

- [x] Fetch seam has a single method; fetch kind only changes the Accept header in the HTTP adapter
- [x] Fetch policy applies all spec rules unchanged (challenge header, 401/402/403/429, 404/410, 503 challenge page, other non-2xx, size cap, page Content-Type, 2xx challenge page); classification helpers private to it
- [x] Fetch policy is the only consumer of the seam; Importer and image downloader call it
- [x] HTTP adapter keeps transport only (redirects, SSRF guard, timeout, request headers, Content-Length precheck, capped streaming) and always reads the body, capped, for every status
- [x] Challenge marker check (first 4 KB as UTF-8) runs in the policy; no second read in the adapter
- [x] Size cap applied by the policy to every body, including whole bodies handed over by an adapter
- [x] Image downloader's own size check deleted; magic-byte sniffing stays
- [x] Fixture adapter goes through the policy (fixture files, link-shortener redirect, unknown URL → 404)
- [x] WireMock tests resolve the fetch policy from the Import service registration and keep their failure-kind assertions; transport-only cases stay
- [x] `ImportFailure` kinds, endpoint status mapping and API contract unchanged; all existing tests green
